# Volt

**Wireless peripheral battery monitor for Windows.** A tray icon that shows the live battery level of your 2.4G mice, keyboards and headsets — click it and a small flyout lists every device with its percentage and charging state.

Most wireless peripherals never report battery level to Windows. They speak a vendor-private HID protocol that only the manufacturer's software understands, so the OS shows nothing and generic battery widgets see nothing. Volt talks those protocols directly.

```
┌─────────────────────────────┐
│ Volt                     ✕  │
│                             │
│  🖱  ATK X1 SE        100 % │
│     2.4G receiver · in use  │
│     ━━━━━━━━━━━━━━━━━━━━    │
│                             │
│  ⌨  AULA F99           95 % │
│     2.4G receiver · charging│
│     ━━━━━━━━━━━━━━━━━━━     │
│                             │
│ Updated 18:03:12         ⟳  │
└─────────────────────────────┘
```

## Why

I bought an ATK X1 SE mouse and an AULA F99 keyboard. Both are 2.4G wireless, both report nothing to Windows, and neither vendor ships software that puts the level anywhere useful. Existing tools either support a fixed list of models, or misidentify the hardware, or show one device twice when it is connected over both dongle and cable.

Volt is the tool I wanted: two devices, correct names, one row each, live percentages in the tray.

## Features

- **Tray icon with live level** — colour-coded battery (green / amber / red), updates every 5 seconds.
- **Click to open a flyout** — one row per device: name, connection type, charging state, percentage, level bar.
- **One device, one row** — a mouse connected over both its 2.4G dongle and a USB cable still appears once. Volt picks the active connection, preferring the one that is actually charging.
- **Survives device sleep** — many 2.4G devices stop answering when idle. Volt keeps the last successful reading on disk and shows it as `last 17:52` instead of blanking out.
- **Starts with Windows** — optional autostart entry, single instance, re-launching the shortcut reopens the flyout instead of doing nothing.
- **No dependencies** — a single 40 KB executable. No .NET runtime install, no Electron, no installer, no admin rights.

## Install

Download `Volt.exe` from [Releases](../../releases) and run it. That's it.

The tray icon appears in the taskbar. If Windows hides it, open the overflow chevron (`^`) and drag the battery icon onto the taskbar.

To start with Windows, add a shortcut to your startup folder, or add a `REG_SZ` value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` pointing at the exe.

> **Do not run it from a folder marked Low integrity level.** Windows blocks tray icon registration (`Shell_NotifyIcon` returns `ERROR_ACCESS_DENIED`) for low-integrity processes. `%LOCALAPPDATA%`, `%APPDATA%`, Desktop and Program Files are all fine.

## Supported devices

| Device | VID:PID | Interface | Notes |
|---|---|---|---|
| ATK X1 SE | `373B:10C9` | FF02/0002, 17 bytes | 2.4G dongle |
| ATK X1 SE | `373B:1107` | FF02/0002, 17 bytes | USB cable / Nearlink |
| AULA F99 | `3554:FA09` | FF02/0002, 20 bytes | 2.4G dongle |

Both use the Compx radio platform, which is shared by a large family of Chinese peripherals: ATK, VXE, VGN, AULA, LEOBOG, Ajazz, Dareu, G-Wolves and others. Adding a model usually means adding one `Endpoint` with its VID/PID — see [Adding a device](docs/ADDING-A-DEVICE.md).

## How it works

Volt enumerates HID collections through SetupAPI, matches them against a small catalogue of known devices, and sends each one a vendor-private report over overlapped I/O. The reply carries the battery percentage, a charging flag, and often a pack voltage.

Nothing is injected into Explorer, no driver is installed, and no vendor software is required.

Full protocol notes: [docs/PROTOCOLS.md](docs/PROTOCOLS.md).

## Build

Requires only the C# compiler that ships with .NET Framework 4.x — no SDK, no MSBuild, no NuGet.

```bat
build.bat
```

Or by hand:

```bat
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe ^
  /target:winexe /platform:anycpu /optimize+ ^
  /win32icon:app.ico /out:Volt.exe ^
  /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  src\*.cs
```

The source is C# 5 — no interpolation, no `?.`, no `nameof` — so it compiles with the in-box compiler on a clean Windows install.

## Project layout

| File | Purpose |
|---|---|
| `src/HidNative.cs` | SetupAPI enumeration, `hid.dll` caps, overlapped read/write wrapper |
| `src/Protocols.cs` | Compx frame builders, device catalogue |
| `src/Scanner.cs` | Match interfaces to devices, pick one connection, persist last readings |
| `src/Theme.cs` | Colours, fonts, rounded-rect helpers |
| `src/TrayIcon.cs` | Runtime icon rendering (multi-size PNG-in-ICO) |
| `src/MainForm.cs` | Flyout window, device rows, tray wiring |
| `src/Program.cs` | Entry point, single-instance mutex |

## Known limitations

- Windows only. The HID layer is Win32.
- Two devices are hard-coded in the catalogue. Other Compx devices need their VID/PID added.
- Polling every 5 seconds resets the device idle timer, which costs a little battery. Raise the interval in `MainForm` if that matters to you.
- Two battery tools running at once will fight over the same HID handle. Close the vendor software.

## License

MIT — see [LICENSE](LICENSE).
