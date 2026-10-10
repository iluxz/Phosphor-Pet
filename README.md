# phosphor pet

a tiny windows desktop pet inspired by the phosphor slimes in cube grotto

## run

open `bin\Release\net10.0-windows\PhosphorPet.exe` or build the project with

```powershell
dotnet build -c Release
```

## install

run `dist\PhosphorPet-Setup.exe` for a per-user installation with a start menu shortcut, an optional desktop shortcut, and an optional launch-at-startup setting. the installer includes the .net runtime, so it does not need to be installed separately. uninstall from Windows Installed apps or the start menu.

phosphor pet checks for updates when it starts and offers to download and install a newer release. you can also check manually from the pet's right-click menu or the system tray menu. pushing a stable `vX.Y.Z` tag builds and publishes the installer as a GitHub release.

to rebuild the self-contained x64 installer, install Inno Setup 6 and the .NET 10 SDK, then run:

```powershell
.\installer\build-installer.ps1
```

the installer source is in `installer\PhosphorPet.iss`.

## controls

- drag the slime with the left mouse button
- picking up the slime makes it shake and look mad until you let go
- right click for the pet menu; click a sleeping slime to wake it
- choose "add another slime" in the pet menu to spawn another one without relaunching the app
- use the menu to pause wandering, choose a hat, toggle largo, or let it sleep
- use the desktop goose submenu to enable the mode, configure corner doodles and cursor grabs, and set the check and inactivity intervals
- choose "go to sleep" to have the slime settle in the bottom-right corner and breathe while sleeping; use the menu or click it to wake it
- right click the phosphor pet icon in the system tray to enable launch on startup or choose "exit phosphor pet" to quit

desktop goose mode occasionally shows a tiny corner doodle or, after the configured inactivity period, scoots over to the idle cursor, grabs it, and slowly escorts it to a different spot on a monitor. a cursor grab stops as soon as you interact and leaves the cursor at its new spot

the pet stays above other windows and does not take keyboard focus when it starts

licensed under the gnu general public license version 3; see `LICENSE`
