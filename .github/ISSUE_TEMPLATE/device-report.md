---
name: Device report
about: Ask for support for a wireless mouse or keyboard
title: "[device] "
labels: device-support
assignees: ''
---

<!-- 中文提示：这个模板用于申请支持一台新设备。尽量填满前四项，剩下的部分能填就填。
     不知道的字段可以写 "unknown"，不要凭空猜测。 -->

**Model**
<!-- 型号，例如 "ATK X1 SE" / "AULA F99"。写厂商实际标注的名字。 -->

**Connection type**
<!-- 连接方式：2.4G 接收器 / USB 有线 / 蓝牙。一台设备有几种连接就都写上。 -->

**VID:PID**
<!-- finder 里的 VID 和 PID，十六进制 -->

**HID collections with a vendor usage page**
<!-- 用途页 >= 0xFF00 的集合，每行一个：UsagePage/Usage, InputLen, OutputLen, Product name -->

**Request frame that returns data (hex)**
<!-- 能把电量数据取回来的请求报文，十六进制 -->

**Reply (hex)**
<!-- 对应的回包，十六进制 -->

**What the vendor software shows**
<!-- 厂商软件显示的电量百分比，用来核对 -->

**Volt version and result**
<!-- 在 Volt 里的表现：认不出来 / 认错人 / 数值不对 / 崩溃。附版本号 -->

**Anything else**
<!-- 其他补充，比如厂商配置页面的 WebHID 脚本片段 -->
