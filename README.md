# UHK Keymap Autochanger

Windows tray app that automatically switches Ultimate Hacking Keyboard (UHK) keymaps based on the active process.

Korean documentation: [README.ko.md](README.ko.md)

## Scope

- OS: Windows
- Device: UHK80 (right-half communication interface)
- Method: direct UHK HID `SwitchKeymap (0x11)` command

## Features

- Process-based keymap mapping (`Code.exe -> DEV`, etc.)
- Immediate fallback to default keymap for unmapped apps
- Duplicate-send prevention (no resend if target keymap is unchanged)
- Tray menu controls
- `Start with Windows` support (HKCU Run)
- JSON config persistence

## Installation

### Run the published EXE

Run `UhkKeymapAutochanger.exe`.

`self-contained` build does not require a separate .NET runtime install.

### Enable auto-start

Open tray menu and turn on `Start with Windows`.

## Usage

1. Create keymaps (with abbreviations) first in UHK Agent.
2. Run `UhkKeymapAutochanger.exe`.
3. Right-click tray icon, then open `Open Settings`.
4. Save your default keymap and process rules.

Tray menu:
- `Open Settings`
- `Start/Stop Switching`
- `Start with Windows`
- `Exit`

## Config File

- Path: `%AppData%\UhkKeymapAutochanger\config.json`
- Auto-created on first launch

Schema:

```json
{
  "defaultKeymap": "DEF",
  "pollIntervalMs": 250,
  "startWithWindows": true,
  "pauseWhenUhkAgentRunning": true,
  "rules": [
    { "processName": "Code.exe", "keymap": "DEV" },
    { "processName": "chrome.exe", "keymap": "WEB" }
  ]
}
```

Field meanings:
- `defaultKeymap`: fallback keymap abbreviation
- `pollIntervalMs`: active-window polling interval (100~1000)
- `startWithWindows`: auto-start with Windows
- `pauseWhenUhkAgentRunning`: if `true`, pauses switching while UHK Agent is running
- `rules`: process-to-keymap mappings

If the config is invalid, the app creates a backup `*.invalid.json` and regenerates defaults.

## UHK Agent Behavior

- Default is `pauseWhenUhkAgentRunning=true`.
- This reduces HID conflicts while UHK Agent is active.
- If you disable it, switching can work with Agent running, but conflict risk is higher.

## Debug Logging

Run with debug flag:

```powershell
UhkKeymapAutochanger.exe --debug
```

Log path:
- `%LocalAppData%\UhkKeymapAutochanger\debug.log`

## Development Build

Prerequisite:
- .NET 8 SDK

```powershell
dotnet restore
dotnet build UhkKeymapAutochanger.sln -c Release
dotnet test tests\UhkKeymapAutochanger.Tests\UhkKeymapAutochanger.Tests.csproj -c Release
```

## Publish Single EXE

```powershell
dotnet publish .\src\UhkKeymapAutochanger\UhkKeymapAutochanger.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  /p:PublishSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true
```

Output:
- `src\UhkKeymapAutochanger\bin\Release\net8.0-windows\win-x64\publish\UhkKeymapAutochanger.exe`
