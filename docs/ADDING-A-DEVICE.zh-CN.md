# 添加新设备

<sub>[English](ADDING-A-DEVICE.md) · 简体中文</sub>

Volt 的设备目录是一串 `DeviceDefinition` 对象。每个对象对应一台物理设备，并列出能访问到它的那些 HID 端点。

加一个型号通常就是加一条记录。加一个协议系列是一个 `Read` 方法。

---

## 1. 找到端点

跑一次 HID 枚举，找带厂商用途页的集合：

```
VendorId ProductId  UsagePage/Usage  InLen OutLen  Product
373B     10C9       FF02/0002        17    17      NK mouse NANO dongle
3554     FA09       FF02/0002        20    20      2.4G Wireless Receiver
```

要找的是输入长度和输出长度相等、且不为零的集合 —— 那才是查询通道。消费类控制（`000C/0001`）和鼠标（`0001/0002`）集合都不是。

如果同一台物理设备出现在多个 VID/PID 下（接收器、有线、蓝牙），把它们全部作为**同一个**定义下的端点加进去。这样 Volt 无论怎么连都只显示一行。

---

## 2. 取一次读数

发该系列的请求帧，看回包。两个已知系列见 [PROTOCOLS.zh-CN.md](PROTOCOLS.zh-CN.md)。

找百分比字节的办法：在不同电量下各取一次读数，看哪个字节跟着数字走。找充电标志：插着线取一次、拔掉取一次，做 diff。

---

## 3. 添加定义

在 `src/Protocols.cs` 的 `Catalog.Build()` 里加：

```csharp
DeviceDefinition mouse = new DeviceDefinition();
mouse.Key = "atk-compx";                  // 选择协议处理器
mouse.DisplayName = "ATK X1 SE";          // 界面上显示的名字 —— 用真实型号名
mouse.Kind = "鼠标";                       // 决定行图标
mouse.Vendor = "ATK";
mouse.Endpoints = new Endpoint[]
{
    new Endpoint { Vid = 0x373B, Pid = 0x10C9, UsagePage = 0xFF02, Usage = 0x0002,
                   InputLength = 17, OutputLength = 17, Transport = "2.4G 接收器" },
    new Endpoint { Vid = 0x373B, Pid = 0x1107, UsagePage = 0xFF02, Usage = 0x0002,
                   InputLength = 17, OutputLength = 17, Transport = "USB 有线" },
};
list.Add(mouse);
```

`Endpoint.Matches` 会比较 VID、PID、用途页、用途，以及两个报文长度。六项全部对上才算匹配 —— 这就是接收器暴露两个同用途集合时，Volt 不会抓错那一个的原因。

如果新设备说的是已有系列，改到这里就完了。`Key` 已经指向正确的处理器。

---

## 4. 新增协议系列

帧结构不一样时，在 `src/Protocols.cs` 里加一个处理器，并从 `DeviceDefinition.Read` 里路由过去：

```csharp
public static class MyVendor
{
    public static BatteryReading Read(HidInterface iface)
    {
        BatteryReading r = new BatteryReading();

        byte[] req = new byte[iface.OutputLength];
        req[0] = 0x13;
        req[1] = 0xCA;

        byte[] resp = HidQuery.Run(iface, req, 4, 400, 120,
            new Func<byte[], bool>(got => got != null && got.Length >= 7 && got[0] == 0x13));

        if (resp == null) { r.Note = "no reply"; return r; }

        r.Percent = resp[5];
        r.Charge = resp[6] == 0x10 ? ChargeState.Charging : ChargeState.Discharging;
        r.Ok = r.Percent >= 0 && r.Percent <= 100;
        return r;
    }
}
```

`HidQuery.Run` **每次尝试都重新开一个句柄**，并在重试前关掉它。这是故意的：一个超时的重叠读取会一直挂在句柄上，把下一次的回包吞掉，所以在同一个句柄上重试永远失败。关句柄才能取消挂起的 I/O。

---

## 5. 验证

```bat
build.bat
Volt.exe
```

查三件事：

1. 百分比和厂商软件显示的读数一致。
2. 拔掉数据线 —— 那一行应该切换连接方式，而不是多出一行。
3. 让设备闲置几分钟 —— 那一行应该显示 `上次 HH:mm`，而不是变空。

如果名字对不上，多半是匹配太松了。优先用 VID/PID 加报文长度，别用产品字符串；同一型号的不同接收器，产品字符串可能不一样。
