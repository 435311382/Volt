# Security policy

<sub>[简体中文](#安全策略) · English</sub>

## Supported versions

| Version | Supported |
| ------- | --------- |
| 1.0.x   | Yes       |

Volt ships as a single executable rebuilt by GitHub Actions on every push. If you are running a file from anywhere other than this repository's releases, you are not running a version this policy covers.

## Reporting a vulnerability

Please use GitHub's **Report a vulnerability** button under the repository's Security tab. That opens a private advisory visible only to the maintainers. Do not open a public issue for a security problem, and do not include working exploit details in one.

If the private reporting button is unavailable, open an issue titled `Security contact request` with no technical detail, and a maintainer will follow up with a private channel.

Useful reports include the version, what you expected, what happened, and the smallest steps that reproduce it. Hardware-specific findings (crash or hang while parsing a particular vendor reply) are in scope — attach the report bytes in hex.

## Scope

In scope:

- Memory-safety or bounds problems in HID report parsing (`src/HidNative.cs`, `src/Protocols.cs`, `src/Scanner.cs`).
- Crashes or hangs reachable from a device's replies, including from a device you don't own.
- Privilege or persistence behaviour that differs from what this repository documents.
- Anything that makes Volt read or write outside its documented footprint.

Out of scope:

- Device not recognised, wrong percentage, or missing charging state. Those are bugs — use the device report or bug report template.
- Coding-style, documentation, and feature requests.
- Findings that require an attacker who already has code execution or administrator rights on the machine.

## Design facts relevant to review

These are properties of the current code, stated so a reviewer can check them rather than take them on trust:

- **No network access.** The program contains no HTTP, socket, or DNS client code.
- **No update mechanism.** Nothing is downloaded or executed at runtime; updates are a manual replacement of `Volt.exe`.
- **No elevation.** It runs as the invoking user and does not request administrative rights, install a service, or register a driver.
- **No autostart of its own.** It writes no registry run key and no startup-folder entry; autostart is opt-in documentation for the user.
- **One write path.** The only file it writes is `%LOCALAPPDATA%\Volt\last-readings.tsv`, a cache of the last known level per device. It contains device keys and integers, no personal data.
- **HID only.** It opens the vendor-defined collections listed in `src/Protocols.cs` and nothing else. Reads are bounded by the report length the collection declares.

---

# 安全策略

<sub>English · 简体中文</sub>

## 支持的版本

| 版本    | 是否支持 |
| ------- | -------- |
| 1.0.x   | 是       |

Volt 是单个可执行文件，由 GitHub Actions 在每次推送时重新构建。如果你手上的文件不是从本仓库的 Releases 下载的，那它不属于本策略的覆盖范围。

## 上报安全问题

请用仓库 Security 标签页里的 **Report a vulnerability** 按钮。它会开启一份只有维护者能看到的私有安全通告。不要为安全问题开公开 issue，也不要在 issue 里贴可用的漏洞细节。

如果私有上报按钮不可用，就开一个标题为 `Security contact request` 的 issue，里面不要写任何技术细节，维护者会跟进并提供私下沟通的渠道。

一份有用的报告包括：版本号、你期望的行为、实际发生的行为，以及最少的复现步骤。与特定硬件相关的发现（解析某个厂商回包时崩溃或卡死）也在受理范围内 —— 请附上十六进制的回包字节。

## 受理范围

受理：

- HID 报文解析中的内存安全问题或越界（`src/HidNative.cs`、`src/Protocols.cs`、`src/Scanner.cs`）。
- 由设备回包触发的崩溃或卡死，包括你手上没有那台设备的情况。
- 与本仓库文档描述不一致的提权或持久化行为。
- 任何让 Volt 读取或写入其既定范围之外文件的问题。

不受理：

- 设备识别不出来、百分比不对、充电状态缺失。这些是 bug —— 请用设备上报或 bug 报告模板。
- 代码风格、文档和功能建议。
- 需要攻击者已经在本机拥有代码执行权限或管理员权限才能成立的发现。

## 与审查相关的设计事实

以下是当前代码的实际属性，列在这里是为了让审查者可以去核对，而不是只能相信：

- **不联网。** 程序里没有任何 HTTP、套接字或 DNS 客户端代码。
- **无更新机制。** 运行时不会下载或执行任何东西；升级就是手动替换 `Volt.exe`。
- **不提权。** 以当前用户身份运行，不申请管理员权限，不安装服务，不注册驱动。
- **自身不设自启动。** 不写注册表启动项，不写启动文件夹；开机启动是给用户看的手动配置说明。
- **只有一条写盘路径。** 唯一写入的文件是 `%LOCALAPPDATA%\Volt\last-readings.tsv`，内容是每台设备最后一次读数的缓存。里面只有设备键和整数，没有个人信息。
- **只用 HID。** 只打开 `src/Protocols.cs` 里列出的厂商自定义集合，不碰别的。读取长度以集合声明的报文长度为准。
