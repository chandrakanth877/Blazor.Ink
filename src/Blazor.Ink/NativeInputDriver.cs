using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Blazor.Ink;

internal abstract class NativeInputDriver : IDisposable
{
    internal abstract void EnableRawMode();
    public abstract Task<int> ReadAsync(byte[] buffer, CancellationToken cancellation);
    public abstract void Dispose();

    internal static bool IsSupported
    {
        get
        {
            if (OperatingSystem.IsWindows())
                return Windows.GetConsoleMode(Windows.GetStdHandle(-10), out _);
            return (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux()) &&
                RuntimeInformation.ProcessArchitecture is Architecture.X64 or Architecture.Arm64 && Posix.isatty(0) == 1;
        }
    }

    internal static NativeInputDriver Capture()
    {
        if (!IsSupported) throw new NotSupportedException("Native input requires a supported terminal stdin.");
        return OperatingSystem.IsWindows() ? new Windows() : new Posix();
    }

    private sealed class Posix : NativeInputDriver
    {
        // ABI checked by scripts/check-native-input.sh against the target's native headers.
        private readonly byte[] saved = new byte[OperatingSystem.IsMacOS() ? 72 : 60];
        private bool restored;
        [StructLayout(LayoutKind.Sequential)]
        private struct PollFd { public int Fd; public short Events; public short Revents; }
        [DllImport("libc", SetLastError = true)] internal static extern int isatty(int fd);
        [DllImport("libc", SetLastError = true)] private static extern int tcgetattr(int fd, [Out] byte[] state);
        [DllImport("libc", SetLastError = true)] private static extern int tcsetattr(int fd, int action, byte[] state);
        [DllImport("libc")] private static extern void cfmakeraw([In, Out] byte[] state);
        [DllImport("libc", SetLastError = true)] private static extern int poll(ref PollFd fd, nuint count, int timeout);
        [DllImport("libc", SetLastError = true)] private static extern nint read(int fd, [Out] byte[] buffer, nuint count);

        public Posix()
        {
            if (tcgetattr(0, saved) != 0) throw new Win32Exception(Marshal.GetLastPInvokeError(), "tcgetattr failed.");
        }

        internal override void EnableRawMode()
        {
            var raw = (byte[])saved.Clone();
            cfmakeraw(raw);
            if (tcsetattr(0, 0, raw) != 0) throw new Win32Exception(Marshal.GetLastPInvokeError(), "tcsetattr failed.");
        }

        public override Task<int> ReadAsync(byte[] buffer, CancellationToken cancellation) => Task.Run(() =>
        {
            // ponytail: one reader, 20ms poll ceiling; use a cancellation fd if wakeup latency becomes material.
            var fd = new PollFd { Fd = 0, Events = 1 };
            while (true)
            {
                cancellation.ThrowIfCancellationRequested();
                var ready = poll(ref fd, 1, 20);
                if (ready < 0)
                {
                    if (Marshal.GetLastPInvokeError() == 4) continue; // EINTR on supported POSIX ABIs.
                    throw new Win32Exception(Marshal.GetLastPInvokeError(), "poll failed.");
                }
                if (ready == 0) continue;
                cancellation.ThrowIfCancellationRequested();
                var count = read(0, buffer, (nuint)buffer.Length);
                if (count >= 0) return (int)count;
                if (Marshal.GetLastPInvokeError() != 4)
                    throw new Win32Exception(Marshal.GetLastPInvokeError(), "stdin read failed.");
            }
        });

        public override void Dispose()
        {
            if (restored) return;
            if (tcsetattr(0, 0, saved) != 0) throw new Win32Exception(Marshal.GetLastPInvokeError(), "Terminal restoration failed.");
            restored = true;
        }
    }

    private sealed class Windows : NativeInputDriver
    {
        private readonly IntPtr handle = GetStdHandle(-10);
        private readonly uint mode, codePage;
        private bool restored;
        [DllImport("kernel32.dll")] internal static extern IntPtr GetStdHandle(int handle);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool GetConsoleMode(IntPtr handle, out uint mode);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetConsoleMode(IntPtr handle, uint mode);
        [DllImport("kernel32.dll")] private static extern uint GetConsoleCP();
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetConsoleCP(uint codePage);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool ReadFile(IntPtr handle, [Out] byte[] buffer,
            uint count, out uint read, IntPtr overlapped);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenThread(uint access, bool inherit, uint id);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CancelSynchronousIo(IntPtr thread);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);

        public Windows()
        {
            if (!GetConsoleMode(handle, out mode)) throw new Win32Exception(Marshal.GetLastPInvokeError());
            codePage = GetConsoleCP();
        }

        internal override void EnableRawMode()
        {
            // Disable processed/line/echo/quick-edit input; enable extended and virtual-terminal input.
            if (!SetConsoleMode(handle, (mode & ~(1u | 2u | 4u | 0x40u)) | 0x80u | 0x200u) || !SetConsoleCP(65001))
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "Virtual-terminal input setup failed.");
        }

        public override Task<int> ReadAsync(byte[] buffer, CancellationToken cancellation) => Task.Run(() =>
        {
            cancellation.ThrowIfCancellationRequested();
            var thread = OpenThread(1, false, GetCurrentThreadId()); // THREAD_TERMINATE for CancelSynchronousIo.
            if (thread == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastPInvokeError());
            Timer? retry = null;
            try
            {
                // Retry cancellation closes the register-before-ReadFile race without closing caller stdin.
                using (cancellation.Register(() => retry = new Timer(_ => CancelSynchronousIo(thread), null, 0, 10)))
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (!ReadFile(handle, buffer, (uint)buffer.Length, out var read, IntPtr.Zero))
                    {
                        cancellation.ThrowIfCancellationRequested();
                        throw new Win32Exception(Marshal.GetLastPInvokeError(), "Console input read failed.");
                    }
                    return (int)read;
                }
            }
            finally
            {
                if (retry is not null) retry.DisposeAsync().AsTask().GetAwaiter().GetResult();
                CloseHandle(thread);
            }
        });

        public override void Dispose()
        {
            if (restored) return;
            Exception? error = null;
            if (!SetConsoleMode(handle, mode)) error = new Win32Exception(Marshal.GetLastPInvokeError(), "Console mode restoration failed.");
            if (!SetConsoleCP(codePage)) error ??= new Win32Exception(Marshal.GetLastPInvokeError(), "Input code-page restoration failed.");
            if (error is not null) throw error;
            restored = true;
        }
    }
}
