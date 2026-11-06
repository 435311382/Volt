# Changelog

<sub>[简体中文](#更新日志) · English</sub>

All notable changes to Volt are documented here. Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versioning follows [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Changed
- The README states up front that the project is AI-developed, with a section on what the AI wrote and what a human decided. The repository description says so too.
- Documentation is now bilingual. Every English document has a `*.zh-CN.md` counterpart; both are kept in sync by the same pull request.
- Rewrote the platform notes to state plainly that the ATK and AULA frames share a vendor platform but not a layout, and that the ATK voltage field is an open interval.
- Corrected the autostart section: Volt has no built-in autostart, only a documented manual configuration.
- Supported-devices table now has a Connection column, so one physical device with two connections is one row, as the code intended.

## [1.0.0] - 2026-11-05

### Added
- Tray-resident battery monitor for 2.4G wireless mice and keyboards on Windows.
- Support for the ATK X1 SE (Compx 17-byte protocol, dongle and USB cable) and the AULA F99 (20-byte protocol).
- Live percentage in the tray tooltip, plus a popup panel with per-device rows.
- Three-colour levels: green above 35%, amber 16–35%, red at 15% and below.
- Charging state, with `charging` and `full` treated separately from the normal reading.
- Last-known-value cache, so an idle device shows `last HH:mm` instead of a blank row.
- Single-instance mutex; launching a second time brings the panel forward.
- Zero-dependency build: .NET Framework's bundled C# compiler, no SDK, no NuGet.
- GitHub Actions workflow producing a build artifact on every push.

### Notes
- Volt does not start with Windows on its own. Instructions for a manual autostart entry are in the README.
- The program makes no network calls and writes nothing outside `%LOCALAPPDATA%\Volt\`.

---

# 更新日志

<sub>English · 简体中文</sub>

本文件记录 Volt 的所有重要变更。格式遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本号遵循[语义化版本](https://semver.org/lang/zh-CN/)。

## [未发布]

### 变更
- README 开头标注本项目由 AI 开发，并新增一节说明哪些由 AI 编写、哪些由人决定。仓库简介里也写明了。
- 文档改为双语。每份英文文档都有对应的 `*.zh-CN.md`，并在同一个 Pull Request 里同步维护。
- 重写平台说明：明确 ATK 与 AULA 共用厂商平台但帧格式不同，并明确 ATK 的电压字段是开区间。
- 更正开机启动一节：Volt 没有内置自启动，只有一份手动的配置说明。
- 支持设备表增加「连接方式」列，使「一台设备一行」与代码行为一致。

## [1.0.0] - 2026-11-05

### 新增
- 常驻托盘的电量监控，面向 Windows 上的 2.4G 无线鼠标与键盘。
- 支持 ATK X1 SE（Compx 17 字节协议，接收器与 USB 有线两种连接）与 AULA F99（20 字节协议）。
- 托盘提示里显示实时电量，另有弹出面板按设备逐行展示。
- 三档颜色：35% 以上绿色，16–35% 琥珀色，15% 及以下红色。
- 充电状态，其中「充电中」「已充满」与普通读数分开处理。
- 最后一次读数缓存，设备闲置时显示 `上次 HH:mm` 而不是空行。
- 单实例互斥；再次启动会把面板调到前面。
- 零依赖构建：用 .NET Framework 自带的 C# 编译器，不用 SDK，不用 NuGet。
- GitHub Actions 工作流，每次推送都产出构建产物。

### 说明
- Volt 不会自行随 Windows 启动。手动添加开机启动项的步骤写在 README 里。
- 程序不发起任何网络请求，除 `%LOCALAPPDATA%\Volt\` 之外不写任何文件。
