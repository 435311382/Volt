# Volt

<sub>[English](README.md) · [简体中文](README.zh-CN.md)</sub>

[![build](https://github.com/435311382/Volt/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/435311382/Volt/actions/workflows/build.yml)
[![license](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)](#known-limitations)
[![developed by AI](https://img.shields.io/badge/developed%20by-AI-blueviolet.svg)](#how-it-was-built)

**Wireless peripheral battery monitor for Windows.** A tray icon that shows the live battery level of your 2.4G mice, keyboards and headsets — click it and a small flyout lists every device with its percentage and charging state.

Most wireless peripherals never report battery level to Windows. They speak a vendor-private HID protocol that only the manufacturer's software understands, so the OS shows nothing and generic battery widgets see nothing. Volt talks those protocols directly.

> **This project is developed entirely by an AI agent.** Every line of C#, the documentation and the CI configuration were written by AI; a human owner set the scope and tested the result on real hardware. [How it was built](#how-it-was-built) explains the split, because it is the part that affects how you should read the code.

<img src="docs/screenshot.png" alt="Volt flyout showing two devices with their battery levels" width="350">

## Contents

- [Why](#why)
- [Features](#features)
- [Install](#install)
- [Supported devices](#supported-devices)
- [How it works](#how-it-works)
- [How it was built](#how-it-was-built)
- [Build](#build)
- [Test](#test)
- [Project layout](#project-layout)
- [Known limitations](#known-limitations)
- [Contributing](#contributing)
- [License](#license)

## Why

I bought an ATK X1 SE mouse and an AULA F99 keyboard. Both are 2.4G wireless, both report nothing to Windows, and neither vendor ships software that puts the level anywhere useful. Existing tools either support a fixed list of models, or misidentify the hardware, or show one device twice when it is connected over both dongle and cable.

Volt is the tool I wanted: two devices, correct names, one row each, live percentages in the tray.

## Features

- **Tray icon with live level** — colour-coded battery (green above 35%, amber 16–35%, red at 15% or below), refreshed every 5 seconds while the flyout is open and every 45 seconds while it is closed.
- **Click to open a flyout** — one row per device: name, connection type, charging state, percentage, level bar.
- **One device, one row** — a mouse connected over both its 2.4G dongle and a USB cable still appears once. Volt picks the active connection, preferring the one that is actually charging.
- **Survives device sleep** — many 2.4G devices stop answering when idle. Volt keeps the last successful reading on disk and shows it as `last 17:52` instead of blanking out.
- **A level that doesn't jump around** — these devices estimate charge from voltage, so the reading climbs whenever the load drops (unplugging the cable, going idle). Volt treats the level as decrease-only, the way Microsoft's battery guidance requires, so a keyboard that reads 96% unplugged stays at 96% instead of bouncing to 84% and back. See [Reading the percentage](docs/PROTOCOLS.md#reading-the-percentage).
- **Manual autostart** — Volt writes nothing to the registry and has no autostart switch. To start it at login, add a shortcut to `shell:startup`, or a `REG_SZ` value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` pointing at the exe.
- **Single instance** — re-launching the executable reopens the flyout instead of starting a second copy.
- **No dependencies** — one self-contained executable, 41,984 bytes (41 KB). No .NET runtime install, no Electron, no installer, no admin rights.

## Install

Download `Volt.exe` from [Releases](../../releases) and run it. That's it.

The tray icon appears in the taskbar. If Windows hides it, open the overflow chevron (`^`) and drag the battery icon onto the taskbar.

To start with Windows, add a shortcut to the startup folder, or add a `REG_SZ` value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` pointing at the exe.

> **Do not run it from a folder marked Low integrity level.** Windows blocks tray icon registration (`Shell_NotifyIcon` returns `ERROR_ACCESS_DENIED`) for low-integrity processes. `%LOCALAPPDATA%`, `%APPDATA%`, Desktop and Program Files are all fine.

## Supported devices

| Device | Connection | VID:PID | HID endpoint | Report |
|---|---|---|---|---|
| ATK X1 SE | 2.4G dongle | `373B:10C9` | `FF02/0002` | 17 bytes |
| ATK X1 SE | USB cable / Nearlink | `373B:1107` | `FF02/0002` | 17 bytes |
| AULA F99 | 2.4G dongle | `3554:FA09` | `FF02/0002` | 20 bytes |

The two ATK rows are two transports of the same mouse — it shows up once in the flyout, not twice.

Both devices expose a vendor-private collection on usage page `0xFF02`, but they do not speak the same frame. The ATK frame is the 17-byte **Compx** layout, shared by a large family of Chinese peripherals: ATK, VXE, VGN and others. The AULA frame is a 20-byte layout shared by other AULA and LEOBOG models. Adding a model usually means adding one `Endpoint` with its VID/PID — see [Adding a device](docs/ADDING-A-DEVICE.md).

## How it works

Volt enumerates HID collections through SetupAPI, matches them against a small catalogue of known devices, and sends each one a vendor-private report over overlapped I/O. The reply carries the battery percentage, a charging flag, and often a pack voltage.

Nothing is injected into Explorer, no driver is installed, and no vendor software is required.

Full protocol notes: [docs/PROTOCOLS.md](docs/PROTOCOLS.md).

## How it was built

Volt is AI-developed. The entire source tree, this README and its Chinese counterpart, the protocol notes, the build script, the CI workflow and the commit messages were produced by an AI coding agent, and the repository is kept the same way. That is stated up front rather than buried, because it changes what you can reasonably expect from the code.

**Written by AI:** all of `src/`, the documentation in `docs/`, `build.bat`, `.github/workflows/build.yml`, and the release notes.

**Decided by a human:** the scope — two devices, no vendor software, no installer, no driver, no elevation. Which features to refuse: lighting control, DPI and macro editing are out of scope by decision, not by oversight. And the hardware verification: plugging the devices in, reading the vendor utility, and comparing its numbers against Volt's.

**What that means for a reader:** the code is small enough to read in an afternoon. The one part that has automated tests is the battery filter — `test/FilterTests.cs`, 38 cases, no framework needed. Everything else is verified by one person with two devices on one machine. Exotic hardware is more likely to hit a reply the parser has never seen than to expose a mistake the documentation is hiding. Anything not written down in [docs/PROTOCOLS.md](docs/PROTOCOLS.md) was never confirmed against real hardware — treat it as unverified rather than as fact.

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

## Test

```bat
test.bat
```

Compiles `test/FilterTests.cs` together with `src/Filter.cs` and runs it. The test program is a plain console exe with its own `Main`; it prints each case and exits non-zero on the first failure. No framework, no NuGet.

The 38 cases cover the decrease-only rule: ordinary discharge, a level that rebounds after the cable is pulled, a large jump that is genuinely a charge, out-of-range bytes, a drop that must persist before it is believed, and the resync path for a device that was away for minutes. CI runs this before the release build.

## Project layout

| Path | Purpose |
|---|---|
| `src/HidNative.cs` | SetupAPI enumeration, `hid.dll` caps, overlapped read/write wrapper |
| `src/Protocols.cs` | Compx frame builders, device catalogue |
| `src/Filter.cs` | Decrease-only battery filter |
| `src/Scanner.cs` | Match interfaces to devices, pick one connection, persist last readings |
| `src/Theme.cs` | Colours, fonts, rounded-rect helpers |
| `src/TrayIcon.cs` | Runtime icon rendering (multi-size PNG-in-ICO) |
| `src/MainForm.cs` | Flyout window, device rows, tray wiring |
| `src/Program.cs` | Entry point, single-instance mutex |
| `test/FilterTests.cs` | Filter behaviour tests, no framework |
| `docs/` | Protocol notes and the device-adding walkthrough |
| `.github/workflows/build.yml` | CI: runs the tests, then compiles on `windows-latest` |

## Known limitations

- Windows only. The HID layer is Win32.
- Two devices are hard-coded in the catalogue. Other Compx devices need their VID/PID added.
- Polling resets the device idle timer, which costs a little battery. The closed-flyout interval is 45 seconds for that reason; raise it in `MainForm` if it still matters.
- Two battery tools running at once will fight over the same HID handle. Close the vendor software.

## Contributing

A new device report is the most useful contribution. See [CONTRIBUTING.md](CONTRIBUTING.md) for what to send, or open a [device report](../../issues/new?template=device-report.md) directly.

## License

MIT — see [LICENSE](LICENSE).
