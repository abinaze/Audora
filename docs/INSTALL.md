# Install Audora

Audora runs on **Windows 10 (version 1809 or newer) and Windows 11, 64-bit**. You do not need to install
anything else.

## Installer (easiest)

1. Download **Audora-Setup.exe** from the [Releases page](https://github.com/abinaze/Audora/releases/latest).
2. Open it. If Windows SmartScreen says "Windows protected your PC", click **More info**, then **Run anyway**.
   This happens because the installer is not code-signed yet.
3. Follow the steps. You can choose a desktop shortcut and whether Audora starts with Windows.
   No administrator rights are needed.

## Portable

1. Download **Audora-Portable.zip** and unzip it anywhere.
2. Run **Audora.exe**. Nothing is installed and nothing is added to Windows.

## Check your download (optional)

Every release has a **SHA256SUMS.txt** file. In PowerShell:

```powershell
Get-FileHash .\Audora-Setup.exe -Algorithm SHA256
```

The result should match the line for that file.

## Uninstall

- Installed version: Windows **Settings → Apps → Audora → Uninstall**. It asks whether to delete your settings too.
- Portable version: close Audora (right-click its tray icon, **Quit**), then delete the folder.

Your settings and log file are in `%LOCALAPPDATA%\Audora`.

## Run from source

See the [README](../README.md#try-it) if you would rather build it yourself.
