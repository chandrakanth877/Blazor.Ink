using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Blazor.Ink.Checks;

// Windows-only runner. Each child independently compares its console mode and input code page.
internal static class ConPtyInputChecks
{
    [StructLayout(LayoutKind.Sequential)] private struct Coord { public short X, Y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfo
    {
        public uint Size;
        public IntPtr Reserved, Desktop, Title;
        public uint X, Y, XSize, YSize, XChars, YChars, Fill, Flags;
        public ushort Show, ReservedSize;
        public IntPtr ReservedBytes, Input, Output, Error;
    }
    [StructLayout(LayoutKind.Sequential)] private struct StartupInfoEx { public StartupInfo Info; public IntPtr Attributes; }
    [StructLayout(LayoutKind.Sequential)] private struct ProcessInfo { public IntPtr Process, Thread; public uint Id, ThreadId; }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CreatePipe(out IntPtr read, out IntPtr write,
        IntPtr attributes, uint size);
    [DllImport("kernel32.dll")] private static extern int CreatePseudoConsole(Coord size, IntPtr input, IntPtr output,
        uint flags, out IntPtr console);
    [DllImport("kernel32.dll")] private static extern void ClosePseudoConsole(IntPtr console);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool InitializeProcThreadAttributeList(
        IntPtr list, int count, uint flags, ref nuint size);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool UpdateProcThreadAttribute(
        IntPtr list, uint flags, nuint attribute, IntPtr value, nuint size, IntPtr previous, IntPtr returned);
    [DllImport("kernel32.dll")] private static extern void DeleteProcThreadAttributeList(IntPtr list);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CreateProcessW(
        string? application, StringBuilder command, IntPtr processAttributes, IntPtr threadAttributes, bool inherit,
        uint flags, IntPtr environment, string? directory, ref StartupInfoEx startup, out ProcessInfo process);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool ReadFile(IntPtr file, [Out] byte[] buffer,
        uint count, out uint read, IntPtr overlapped);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool WriteFile(IntPtr file, byte[] buffer,
        uint count, out uint written, IntPtr overlapped);
    [DllImport("kernel32.dll")] private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
    [DllImport("kernel32.dll")] private static extern bool GetExitCodeProcess(IntPtr process, out uint code);
    [DllImport("kernel32.dll")] private static extern bool TerminateProcess(IntPtr process, uint code);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    private static void Check(bool success) { if (!success) throw new Win32Exception(Marshal.GetLastPInvokeError()); }

    public static async Task RunAsync()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("ConPTY checks require Windows.");
        foreach (var scenario in new[] { "normal", "release", "ctrl-c", "cancel", "callback-failure", "startup-failure",
            "mount-input", "preconfigured", "component-failure", "competing", "output-only" })
        {
            IntPtr inputRead = IntPtr.Zero, inputWrite = IntPtr.Zero, outputRead = IntPtr.Zero, outputWrite = IntPtr.Zero;
            IntPtr console = IntPtr.Zero, attributes = IntPtr.Zero;
            var process = new ProcessInfo();
            Task? pump = null;
            var output = new StringBuilder();
            try
            {
                Check(CreatePipe(out inputRead, out inputWrite, IntPtr.Zero, 0));
                Check(CreatePipe(out outputRead, out outputWrite, IntPtr.Zero, 0));
                Marshal.ThrowExceptionForHR(CreatePseudoConsole(new() { X = 80, Y = 24 }, inputRead, outputWrite, 0, out console));
                CloseHandle(inputRead); inputRead = IntPtr.Zero;
                CloseHandle(outputWrite); outputWrite = IntPtr.Zero;
                nuint size = 0;
                InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
                attributes = Marshal.AllocHGlobal(checked((nint)size));
                Check(InitializeProcThreadAttributeList(attributes, 1, 0, ref size));
                Check(UpdateProcThreadAttribute(attributes, 0, 0x20016, console, (nuint)IntPtr.Size, IntPtr.Zero, IntPtr.Zero));
                var startup = new StartupInfoEx { Attributes = attributes };
                startup.Info.Size = (uint)Marshal.SizeOf<StartupInfoEx>();
                var command = new StringBuilder($"\"{Environment.ProcessPath}\" \"{typeof(ConPtyInputChecks).Assembly.Location}\" --native-input-child {scenario}");
                Check(CreateProcessW(null, command, IntPtr.Zero, IntPtr.Zero, false, 0x80000, IntPtr.Zero, null, ref startup, out process));
                pump = Task.Run(() =>
                {
                    var buffer = new byte[4096];
                    var decoder = Encoding.UTF8.GetDecoder();
                    var chars = new char[4098];
                    var sent = false;
                    while (ReadFile(outputRead, buffer, (uint)buffer.Length, out var read, IntPtr.Zero) && read > 0)
                    {
                        output.Append(chars, 0, decoder.GetChars(buffer, 0, (int)read, chars, 0));
                        if (output.Length > 1_000_000) throw new Exception("ConPTY output exceeded test limit.");
                        if (!sent && output.ToString().Contains("NATIVE_READY", StringComparison.Ordinal))
                        {
                            sent = true;
                            var text = scenario switch
                            {
                                "callback-failure" => "x", "ctrl-c" => "\x03", "cancel" => "",
                                _ => "\x1b[200~p\r\u0003\x1b[201~q"
                            };
                            var input = Encoding.UTF8.GetBytes(text);
                            if (input.Length > 0)
                            {
                                Check(WriteFile(inputWrite, input, (uint)input.Length, out var written, IntPtr.Zero));
                                if (written != input.Length) throw new Exception("Incomplete ConPTY fixture write.");
                            }
                        }
                    }
                });
                if (await Task.Run(() => WaitForSingleObject(process.Process, 15_000)) != 0)
                    throw new TimeoutException($"ConPTY {scenario} timed out.");
                Check(GetExitCodeProcess(process.Process, out var code));
                ClosePseudoConsole(console); console = IntPtr.Zero;
                await pump.WaitAsync(TimeSpan.FromSeconds(5));
                if (code != 0 || !output.ToString().Contains("NATIVE_PASS " + scenario, StringComparison.Ordinal))
                    throw new Exception($"ConPTY {scenario}: exit {code}\n{output}");
                if (scenario == "output-only" && output.ToString().Contains("\x1b[?2004", StringComparison.Ordinal))
                    throw new Exception("Output-only session negotiated input protocols.");
                Console.WriteLine($"PASS ConPTY {scenario}");
            }
            finally
            {
                if (process.Process != IntPtr.Zero && WaitForSingleObject(process.Process, 0) != 0)
                    TerminateProcess(process.Process, 1);
                if (console != IntPtr.Zero) ClosePseudoConsole(console);
                if (pump is not null) try { await pump.WaitAsync(TimeSpan.FromSeconds(5)); } catch { }
                foreach (var handle in new[] { inputRead, inputWrite, outputRead, outputWrite, process.Process, process.Thread })
                    if (handle != IntPtr.Zero) CloseHandle(handle);
                if (attributes != IntPtr.Zero) { DeleteProcThreadAttributeList(attributes); Marshal.FreeHGlobal(attributes); }
            }
        }
    }
}
