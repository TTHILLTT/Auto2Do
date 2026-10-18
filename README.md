# Auto2Do

Auto2Do is a small Windows desktop app for scheduling shutdown, restart, and workstation lock tasks.

## Windows 10 and newer

- Requires Windows and the .NET 10 SDK.
- Run from this folder with `dotnet run --project Auto2Do.csproj`.
- Build a self-contained x64 release with `dotnet publish Auto2Do.csproj -c Release -r win-x64 --self-contained true -o dist\win10-x64`.
- Open **帮助 → 关于 Auto2Do** to see project attribution and repository information.

## Windows 7 x64

- The separate `Auto2Do.Win7.csproj` build targets .NET Framework 4.8 and 64-bit Windows.
- Build it with `dotnet publish Auto2Do.Win7.csproj -c Release -p:PlatformTarget=x64 -o dist\win7-x64`.
- Windows 7 SP1 with .NET Framework 4.8 installed is required. This framework-dependent build is not self-contained.
- This build was compiled and smoke-tested on a newer Windows version; it has not yet been exercised on a Windows 7 machine.
- Open **帮助 → 关于 Auto2Do** to see project attribution and repository information.

## Command-line editions

Both command-line editions share the same task file as the desktop app:

- Windows 10 and newer: `dotnet publish Auto2Do.Cli.csproj -c Release -r win-x64 --self-contained true -o dist\win10-cli-x64`
- Windows 7 x64: `dotnet publish Auto2Do.Cli.Win7.csproj -c Release -p:PlatformTarget=x64 -o dist\win7-cli-x64`
- The Windows 7 edition requires Windows 7 SP1 and .NET Framework 4.8.
- Run `Auto2Do.Cli.exe help` to see all commands. Use `list`, `add`, `remove`, `enable`, `disable`, and `cancel` to manage schedules.
- Run `Auto2Do.Cli.exe about` to print project attribution, model, and repository URL.
- Start `Auto2Do.Cli.exe daemon` in a console and leave it running to execute scheduled tasks. Press Ctrl+C to stop it.
- Optionally set `AUTO2DO_TASKS_FILE` to use a different task JSON file; normally both editions use `%LOCALAPPDATA%\Auto2Do\tasks.json`, shared with the desktop app.

The app's saved tasks are stored in `%LOCALAPPDATA%\Auto2Do\tasks.json`. Enable **登录 Windows 时自动启动** to have the app run in the system tray after you sign in. Use **深色模式 (AMOLED)** to switch to a black-background theme; this preference is saved for your Windows account. The app must be running for schedules to fire. If Windows is asleep or the app is closed during the scheduled minute, the task is skipped rather than run late.

One-time, daily, and weekly schedules are supported. Shutdown and restart are initiated with a 60-second delay, which can be canceled using the in-app button or `shutdown /a`. Locking the workstation is immediate.
