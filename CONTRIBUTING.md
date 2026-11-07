# Contributing

<sub>[English](CONTRIBUTING.md) · [简体中文](CONTRIBUTING.zh-CN.md)</sub>

Small project, small process.

## Reporting a device

The most useful thing you can send is a new device report. Include:

- The model name and how you connect it (2.4G dongle / USB cable / Bluetooth).
- The VID:PID, plus every HID collection with a vendor usage page (`>= 0xFF00`) and its input/output report lengths.
- A hex dump of a request that returns battery data, and the reply.
- What the vendor software reports, so the percentage can be checked.

You can get the first two with any HID enumeration tool. `docs/ADDING-A-DEVICE.md` explains which collection matters and how to read the reply.

The [device report template](.github/ISSUE_TEMPLATE/device-report.md) asks for exactly these fields.

## Pull requests

- Keep the build dependency-free. Volt compiles with the C# compiler that ships with .NET Framework 4.x — no SDK, no NuGet, no MSBuild.
- The source is C# 5. No string interpolation, no null-conditional operator, no `nameof`. The in-box compiler on a clean Windows install does not support them.
- One device per commit where practical, so the catalogue stays reviewable.
- Say how you tested it. "Reads 87% on my F99, matches the vendor software" is enough.
- If you touch `src/Filter.cs`, run `test.bat` and add a case for the behaviour you changed. The filter is the one part where a regression is invisible on screen — the number just drifts the wrong way.

## Style

- Comments explain why, not what. A comment that restates the next line should be deleted.
- Hard-won knowledge belongs in a comment next to the code that depends on it — the interface-ordering trap, the sleep behaviour, the checksum that is not a checksum. Those are the parts a reader cannot recover by reading the code alone.
- **Documentation is bilingual.** Every document has an English main file and a `*.zh-CN.md` translation, linked from a language switcher at the top. Change both in the same pull request; a translation that lags behind the English text is a bug.
- **The application interface and code comments are Simplified Chinese.** The tray menu, the flyout labels and the protocol notes people read while debugging are written in the language the devices' own software is localised into, and the comments match the source they sit in.
- **Commit messages are English**, one line, imperative mood.
- Protocol notes may quote vendor strings verbatim.

## Scope

Volt does one thing: show battery levels for wireless peripherals in the tray. Features that are not that — lighting control, DPI settings, macro editing — belong in the vendor software.
