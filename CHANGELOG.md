# Changelog

<sub>[简体中文](#更新日志) · English</sub>

All notable changes to Volt are documented here. Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versioning follows [Semantic Versioning](https://semver.org/).

## [1.2.0] - 2026-11-08

### Added
- **A device table instead of a hard-coded pair.** `src/DeviceTable.cs` now holds 137 models across 28 vendors — 60 mice, 54 headsets, 13 keyboards, 7 receivers, 3 controllers — with 217 wireless product IDs and 58 cable product IDs. The table is keyed on VID:PID; a model that exposes both a dongle and a cable is one row, not two.
- **A protocol registry instead of an `if` chain.** `src/ProtocolsVendor.cs` holds 23 vendor protocols plus a standard-HID fallback, each with a `Matches` predicate and a priority. `ProtocolRegistry.Probe` walks them in order and the first claimant gets the device, so adding a family no longer means editing a dispatcher.
- **Feature-report support.** `HidNative.Handle` gained `SetFeature` / `GetFeature` / `SetOutputReport` / `GetInputReport`, and `HidQuery` gained `RunFeature` and `RunPush`. Razer, ASUS, Lamzu, Lenovo and the Logitech headsets all speak over feature reports; Sony, Audeze, Jabra, Corsair Virtuoso and SteelSeries only push.
- **Headsets.** Corsair, Jabra, Lenovo, Plantronics, Audeze, SteelSeries, Sony and MCHOSE families, ported from [HeadsetControl](https://github.com/Sapd/HeadsetControl).
- **Left, right and case.** `BatteryReading` carries `LeftPercent`, `RightPercent` and `CasePercent` in addition to `Percent`, and the flyout shows `L 88% R 87% C 100%` when more than one is present. Every open-source implementation reviewed collapses a TWS headset to one number — HeadsetControl parses all three Sony bytes and then returns `Math.Min(left, right)`.
- **Earbud icon.** True-wireless models draw a pair of earbuds instead of a headset.
- **A note field per model.** Devices that genuinely cannot report a level say so in the row instead of showing a blank or a fabricated number.
- `docs/DEVICES.md`: the full table, generated from `src/DeviceTable.cs` so it cannot drift.

### Changed
- The scanner now groups HID collections by VID:PID and asks the registry, instead of walking a fixed catalogue and matching endpoints.
- `DeviceTable.Fallback` only invents a row for unknown hardware when the device exposes the standard HID battery page (`0x0006` / `0x0020`). Unknown vendors without it are skipped, so cameras and card readers stay out of the list.
- The flyout builds its rows from what was actually detected, caps at 7 visible rows and scrolls beyond that.
- The README's supported-devices section is now a per-vendor summary pointing at `docs/DEVICES.md`, and the known-limitations section explains why most 2.4G audio dongles report nothing.

### Fixed
- **The 17-byte Compx frame was being sent to the 20-byte collection.** The ATK dongle exposes both on usage page `FF02/0002`, and the enumeration order puts the 20-byte one first. Every write was rejected and the mouse looked unsupported. `HidGroup.ByUsage` now takes the expected output length as a required argument.
- Long device names were hard-clipped mid-letter in the row; they now ellipsize.

## [1.1.0] - 2026-11-08

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

## [1.2.0] - 2026-11-08

### 新增
- **用设备表取代硬编码的两台设备。** `src/DeviceTable.cs` 现在收录 137 个型号、28 个厂商 —— 60 只鼠标、54 副耳机、13 把键盘、7 个接收器、3 个手柄 —— 共 217 个无线产品 ID、58 个有线产品 ID。表以 VID:PID 为键；同时有接收器和有线的型号只占一行，不是两行。
- **用协议注册表取代 `if` 分发。** `src/ProtocolsVendor.cs` 里有 23 套厂商协议加一个标准 HID 兜底，每套带 `Matches` 判断和优先级。`ProtocolRegistry.Probe` 按顺序走一遍，第一个认领的拿走设备，所以加协议族不用再改分发器。
- **支持 Feature 报文。** `HidNative.Handle` 增加 `SetFeature` / `GetFeature` / `SetOutputReport` / `GetInputReport`，`HidQuery` 增加 `RunFeature` 和 `RunPush`。Razer、ASUS、Lamzu、Lenovo 和罗技耳机都走 Feature 报文；Sony、Audeze、Jabra、Corsair Virtuoso 和 SteelSeries 只等推送。
- **耳机。** Corsair、Jabra、Lenovo、Plantronics、Audeze、SteelSeries、Sony、MCHOSE 各系列，从 [HeadsetControl](https://github.com/Sapd/HeadsetControl) 移植。
- **左耳、右耳、充电盒。** `BatteryReading` 除 `Percent` 外还带 `LeftPercent`、`RightPercent`、`CasePercent`，多路存在时浮窗显示 `L 88% R 87% C 100%`。审过的每一个开源实现都把真无线耳机压成一个数 —— HeadsetControl 明明解析出 Sony 的三路字节，最后返回的是 `Math.Min(left, right)`。
- **耳塞图标。** 真无线型号画两只耳塞，不再用头戴式耳机图标。
- **每个型号一个说明字段。** 确实读不到电量的设备会如实写在行里，而不是显示空白或编一个数字。
- `docs/DEVICES.md`：完整设备表，由 `src/DeviceTable.cs` 生成，不会与代码脱节。

### 变更
- 扫描器改为按 VID:PID 把 HID 集合分组再问注册表，不再走固定目录匹配端点。
- `DeviceTable.Fallback` 只在这台未知设备暴露标准 HID 电池页（`0x0006` / `0x0020`）时才给它建一行。没有这个页的未知厂商直接跳过，摄像头和读卡器不会跑进列表。
- 浮窗按实际探测到的设备建行，最多显示 7 行，超出滚动。
- README 的支持设备一节改成按厂商汇总并指向 `docs/DEVICES.md`，已知限制里说明了为什么大多数 2.4G 音频接收器读不到东西。

### 修复
- **17 字节的 Compx 帧被发到了 20 字节的集合上。** ATK 接收器在用途页 `FF02/0002` 上把两个集合都暴露出来，而枚举顺序把 20 字节那个排在前面。每次写入都被拒，鼠标看起来就像不支持。`HidGroup.ByUsage` 现在把期望的输出长度做成必填参数。
- 设备名过长时会在字母中间被硬裁，现在改用省略号。

## [1.1.0] - 2026-11-08

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
