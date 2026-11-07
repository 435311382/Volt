# Changelog

<sub>[简体中文](#更新日志) · English</sub>

All notable changes to Volt are documented here. Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versioning follows [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added
- `src/Filter.cs`: a decrease-only battery filter, plus 38 unit tests in `test/FilterTests.cs`. Vendor firmware estimates charge from voltage, so the reported level rises whenever the load drops — unplugging the cable, going idle. Microsoft's accessory guidance requires battery level to be a decrease-only attribute, increasing only on a charger or after a battery swap; the filter implements that, with a persisted-drop guard for genuinely bad frames and a five-minute resync so a stale value cannot get stuck.
- Voltage replies are now verified by checksum instead of prefix alone. Unrelated traffic on the same endpoint can start with `08 04` and put garbage in the percentage byte — 116, 123 and 127 were all observed.
- AULA replies are matched in both observed shapes: `13 4A 01 00 02` (percentage at 5) and `13 0A 01 00 04 05` (percentage at 6). The second shape was previously treated as silence.
- `docs/PROTOCOLS.md` gained a "Reading the percentage" section covering the voltage-sag mechanism, the Microsoft rule, and why smoothing is the wrong fix.

### Changed
- Polling no longer stops when the flyout is closed. It drops from 5 s to 45 s instead, so the tray icon keeps up to date; 45 s is deliberately above the 30–60 s sleep timer these keyboards use, so the idle poll does not keep the device awake.
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

### 新增
- `src/Filter.cs`：只降不升的电量过滤器，另有 `test/FilterTests.cs` 里 38 个单元测试。厂商固件靠电压估算电量，负载一降上报值就往上涨 —— 拔线、闲置都会。微软配件规范要求电量是只降不升的属性，只有接充电器或换过电池才允许上升；过滤器实现这条规则，并附带两道保护：大幅下跌需要持续确认，防止单个坏帧把显示永久锁死；超过五分钟重新同步，防止陈旧值卡住。
- 电压回包改为校验和验证，不再只看前缀。同一接口上的无关流量可能以 `08 04` 开头，把垃圾值塞进电量字节 —— 实测见过 116、123、127。
- AULA 回包现在两种形状都认：`13 4A 01 00 02`（电量在下标 5）与 `13 0A 01 00 04 05`（电量在下标 6）。第二种以前会被当成没应答。
- `docs/PROTOCOLS.md` 新增「电量怎么读」一节，讲电压压降的机理、微软的规则，以及为什么平滑是错的解法。

### 变更
- 面板收起后不再停止轮询，而是把间隔从 5 秒放慢到 45 秒，托盘图标因此能保持更新；45 秒刻意高于这类键盘 30–60 秒的休眠计时器，避免空闲轮询把设备一直唤醒。
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
