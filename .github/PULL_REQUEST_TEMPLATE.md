<!-- 中文提示：PR 描述用英文写（与提交信息一致），这里保留中英双语的检查项。 -->

## What changed

<!-- 一句话说明改了什么 -->

## Why

<!-- 动机：修的 bug、加的设备、补的文档 -->

## How it was tested

<!-- 「在我自己的 F99 上读到 87%，和厂商软件一致」这种就够 -->

## Checklist

- [ ] `build.bat` still compiles with the bundled .NET Framework compiler (no SDK, no NuGet).
- [ ] Source stayed C# 5 — no string interpolation, no `?.`, no `nameof`.
- [ ] If documentation changed, the English file and its `*.zh-CN.md` counterpart were updated in the same PR. / 文档改动时英文与中文版同步更新。
- [ ] If a device was added, its name, transport and report lengths were verified against the real hardware. / 新增设备时已用真机核对名称、连接方式与报文长度。
