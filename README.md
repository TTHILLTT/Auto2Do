# Auto2Do

Auto2Do is a small Windows desktop app for scheduling shutdown, restart, and workstation lock tasks.

## Build and run

- Requires Windows and the .NET 10 SDK.
- Run from this folder with `dotnet run`.
- Build a Windows executable with `dotnet publish -c Release -r win-x64 --self-contained true`.

The app's saved tasks are stored in `%LOCALAPPDATA%\Auto2Do\tasks.json`. Enable **登录 Windows 时自动启动** to have the app run in the system tray after you sign in. The app must be running for schedules to fire. If Windows is asleep or the app is closed during the scheduled minute, the task is skipped rather than run late.

One-time, daily, and weekly schedules are supported. Shutdown and restart are initiated with a 60-second delay, which can be canceled using the in-app button or `shutdown /a`. Locking the workstation is immediate.
