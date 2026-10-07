#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet="${DOTNET:-dotnet}"
case "$(uname -s)" in
  Darwin|Linux) ;;
  *) echo "Use the Blazor.Ink.Checks --conpty-input entrypoint on Windows."; exit 1 ;;
esac
mkdir -p artifacts/native-input
cat > artifacts/native-input/abi.c <<'EOF'
#include <errno.h>
#include <stddef.h>
#include <stdio.h>
#include <termios.h>
#include <poll.h>
#include <fcntl.h>
int main(void) {
#ifdef __APPLE__
    int valid = sizeof(struct termios) == 72 && sizeof(tcflag_t) == 8 &&
        offsetof(struct termios, c_cc) == 32 && offsetof(struct termios, c_ispeed) == 56 &&
        offsetof(struct termios, c_ospeed) == 64 && NCCS == 20 && VMIN == 16 && VTIME == 17 &&
        PENDIN == 0x20000000 && offsetof(struct termios, c_lflag) == 24;
#else
    int valid = sizeof(struct termios) == 60 && sizeof(tcflag_t) == 4 &&
        offsetof(struct termios, c_cc) == 17 && offsetof(struct termios, c_ispeed) == 52 &&
        offsetof(struct termios, c_ospeed) == 56 && NCCS == 32 && VMIN == 6 && VTIME == 5;
#endif
    valid = valid && TCSANOW == 0 && EINTR == 4 && POLLIN == 1 && sizeof(struct pollfd) == 8 && F_GETFL == 3;
    printf("termios size=%zu cc=%zu ispeed=%zu ospeed=%zu NCCS=%d VMIN=%d VTIME=%d\n",
        sizeof(struct termios), offsetof(struct termios, c_cc), offsetof(struct termios, c_ispeed),
        offsetof(struct termios, c_ospeed), NCCS, VMIN, VTIME);
    return valid ? 0 : 1;
}
EOF
cc artifacts/native-input/abi.c -o artifacts/native-input/abi
artifacts/native-input/abi
for framework in net8.0 net10.0; do
  "$dotnet" build tests/Blazor.Ink.Checks -c Release -f "$framework" --no-restore -p:UseSharedCompilation=false -m:1
  python3 scripts/check-native-input.py "$dotnet" "tests/Blazor.Ink.Checks/bin/Release/$framework/Blazor.Ink.Checks.dll"
done
