<p align="center">
  <img src="desktop/upkeep/branding/upkeep-mark.svg" alt="Upkeep icon" width="112" height="112">
</p>

<h1 align="center">Upkeep</h1>
<p align="center">Keep Windows 11 software up to date from one focused app.</p>
<p align="center"><a href="https://github.com/SeanShaffer88/Upkeep/releases/latest">Download the latest release</a></p>

Upkeep is a Windows 11 x64 desktop app for checking and installing software updates through supported package managers. It is based on UniGetUI's update workflow, with unrelated discovery and catalog screens removed.

## Features

- Check, select, and install available package updates, including administrator-elevated installers when needed.
- Schedule update checks and optional automatic installs.
- Export an installed-software inventory to JSON and compare it with another Upkeep inventory.
- Scan portable app folders and review possible package-manager matches. Upkeep does not replace files from arbitrary download links.
- Review update operation history and export recent update details for troubleshooting.

## Install

Download `*-Setup.exe` to install Upkeep, or `*-Portable.zip` to run it without installing. Both packages are for Windows 11 x64. Use `SHA256SUMS.txt` to verify the downloaded files.

## Troubleshooting

Run `Upkeep.exe -debug` from PowerShell or Command Prompt to create a recent-update report on your Desktop (or in the temporary folder if the Desktop is unavailable). It replaces common credential values and user-profile paths; review the file before sharing it.

## Build from source

Requires the .NET 10 SDK and Windows 11 x64:

```powershell
dotnet publish .\desktop\upkeep\src\UniGetUI.Avalonia\UniGetUI.Avalonia.csproj `
  -c Release -r win-x64 -p:Platform=x64
```

Release tags use `upkeep-v<version>` and must match `desktop/upkeep/seanpak.json`. The release workflow packages Setup and portable builds with [SeanPak](https://github.com/SeanShaffer88/SeanPak).

## License and upstream

Upkeep is an independent derivative of [UniGetUI](https://github.com/marticliment/UniGetUI), not affiliated with or endorsed by its authors. See `LICENSE` and `desktop/upkeep/UPSTREAM-LICENSE.txt` for licensing details.