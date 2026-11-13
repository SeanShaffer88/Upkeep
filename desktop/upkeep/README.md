# Upkeep

Upkeep is a Windows 11 x64 desktop app for finding and installing software updates. Its update screen and package-update engine are derived from [UniGetUI](https://github.com/marticliment/UniGetUI); the rest of UniGetUI's user-facing pages are not exposed.

Download the latest release from [GitHub Releases](https://github.com/SeanShaffer88/Upkeep/releases/latest). Use the Setup executable to install, or the Portable ZIP to run without installation. Packages are x64-only; verify downloads with `SHA256SUMS.txt`.

## Build

Requires the .NET 10 SDK and Windows 11 x64.

```powershell
dotnet publish .\src\UniGetUI.Avalonia\UniGetUI.Avalonia.csproj `
  -c Release -r win-x64 -p:Platform=x64
```

The release workflow packages the published app with [SeanPak](https://github.com/SeanShaffer88/SeanPak). Release tags use `upkeep-v<version>` and must match the `version` in `seanpak.json`.

## Upstream and licensing

Upkeep is based on UniGetUI's Avalonia application and package engine, currently imported from upstream commit `07300b221576a1cbfdce3b3080bd5db16189fa17`. The upstream MIT license is retained in `UPSTREAM-LICENSE.txt`. Upkeep is an independent derivative and is not affiliated with or endorsed by the UniGetUI authors.

This build is intentionally limited to the software-update workflow and Windows x64. It does not provide UniGetUI's discovery, installed-app browsing, catalog, bundle, repository, or cross-platform screens.

## Update-only features

- Package-manager updates are listed with their versions and sources. Batch update, per-package ignore rules, integrity-check options, administrator elevation, and operation history are retained from UniGetUI.
- Update preferences expose scheduled checks and optional scheduled installs without opening unrelated settings pages.
- Installed package inventories can be exported to JSON and compared with another Upkeep inventory; comparison does not install or remove software.
- Portable folders can be selected and scanned for executable version metadata. Upkeep shows possible WinGet matches for review, but it does not replace files for an unregistered executable based on a name match.
- GitHub portable-topic searches and PortableFreeware are external discovery references only. A topic or directory listing is not trusted update metadata; actual updates must be available through a supported package manager.
- Upkeep keeps its own settings and machine-wide data separate from UniGetUI. The app itself is released through SeanPak packages, not UniGetUI's self-update feed.

The portable folder scanner is a discovery aid. Arbitrary GitHub or PortableFreeware download-and-replace flows are not enabled.

## Troubleshooting

Run `Upkeep.exe -debug` from PowerShell or Command Prompt to export the latest package-update records to a timestamped text file on your Desktop. If the Desktop cannot be used, the file is saved in the temporary folder. The report includes installer output and basic redaction for credentials and user-profile paths; review it before sharing.
