# 添加新设备

<sub>[English](ADDING-A-DEVICE.md) · 简体中文</sub>

Volt 把设备支持拆成两个文件：

- `src/DeviceTable.cs` —— 型号表。回答「`VID:PID` 是哪台物理设备」，管名字、类型、厂商，以及每种连接方式对应的产品 ID。
- `src/ProtocolsVendor.cs` —— 协议。回答「怎么问它要电量」，一个协议族一个类。

加一个型号是在第一个文件里加一行。加一个协议族是在第二个文件里加一个类。

---

## 1. 找到端点

跑一次 HID 枚举，找带厂商用途页的集合：

```
VendorId ProductId  UsagePage/Usage  InLen OutLen  Product
373B     10C9       FF02/0002        17    17      NK mouse NANO dongle
373B     10C9       FF02/0002        20    20      NK mouse NANO dongle
3554     FA09       FF02/0002        20    20      2.4G Wireless Receiver
```

要找的是输入长度和输出长度相等、且不为零的集合 —— 那才是查询通道。消费类控制（`000C/0001`）和鼠标（`0001/0002`）集合都不是。

**注意长度。** 上面那只 ATK 接收器在同一个用途页上暴露了**两个**集合，只有 17 字节那个认 Compx 帧。抓第一个匹配就会拿到 20 字节那个，每次写入都失败，设备看起来就像「不支持」。这个错误容易犯、也难看出来，所以 `HidGroup.ByUsage(page, usage, outputLength)` 把长度做成了必填参数。

如果同一台物理设备出现在多个 VID/PID 下（接收器、有线、蓝牙），把它们全部列进**同一个** `DeviceModel`：`Wireless` 放接收器的 PID，`Wired` 放有线的 PID。这样 Volt 无论怎么连都只显示一行。

---

## 2. 取一次读数

发该系列的请求帧，看回包。已经实现过的格式见 [PROTOCOLS.zh-CN.md](PROTOCOLS.zh-CN.md)。

找百分比字节的办法：在不同电量下各取一次读数，看哪个字节跟着数字走。找充电标志：插着线取一次、拔掉取一次，做 diff。

现在有三种传输方式，互不通用：

| 方式 | 辅助方法 | 适用 |
|---|---|---|
| 发 Output 报文再读 | `HidQuery.Run` | 大多数鼠标键盘。写一条请求，等回包。 |
| Feature 报文往返 | `HidQuery.RunFeature` | Razer、ASUS、Lamzu、Lenovo。写 Feature 报文，等一下，再读回来。零权限句柄也能用，所以 Windows 独占设备时照样工作。 |
| 只等推送 | `HidQuery.RunPush` | Sony、Audeze、Jabra、Corsair Virtuoso、SteelSeries。接收器会周期性自己吐，你只管听。 |

---

## 3. 添加型号

在 `src/DeviceTable.cs` 的 `All()` 里加一行：

```csharp
Add(m, "atk-x1-se", "ATK X1 SE", "鼠标", "ATK", 0x373B,
    W(0x10C9), W(0x1107));
```

参数依次是：列表、key、显示名、类型、厂商、VID、无线 PID、有线 PID。后面还有两个可选参数：

```csharp
Add(m, "sony-inzone-buds", "Sony INZONE Buds", "耳机", "Sony", 0x054C,
    W(0x0EC2), new ushort[0], true);
```

`true` 表示这是真无线耳塞，行图标会从头戴式耳机换成两只耳塞。最后一个参数是拿不到电量时显示在副标题里的说明：

```csharp
Add(m, "baseus-aequr-g10", "Baseus AeQur G10", "耳机", "Baseus", 0x10D6,
    W(0x4801), new ushort[0], true, "2.4G 接收器不上报电量");
```

PID 列表用 `W(...)`，它收 `int`，可以直接写十六进制不用强转。

`key` 是身份标识，落盘缓存和 `%LOCALAPPDATA%\Volt\names.tsv` 里的改名都按它走。发出去之后就别再改了，改了用户的缓存读数会丢。

如果新设备说的是已经实现的协议族，改到这里就完了。`ProtocolRegistry.Probe` 会按优先级顺序走一遍，第一个 `Matches` 返回 true 的协议拿走这台设备。

---

## 4. 新增协议族

在 `src/ProtocolsVendor.cs` 里继承 `BatteryProtocol`：

```csharp
internal sealed class MyVendorProtocol : BatteryProtocol
{
    public override string Name { get { return "MyVendor"; } }

    // 数字小的先跑。Compx 是 30，通用的标准 HID 电池兜底是 90。
    public override int Priority { get { return 55; } }

    public override bool Matches(HidGroup group)
    {
        if (group.Vid != 0x1234) return false;
        return group.ByUsage(0xFF00, 0x0001, 8) != null;
    }

    public override BatteryReading Probe(HidGroup group)
    {
        HidInterface iface = group.ByUsage(0xFF00, 0x0001, 8);
        if (iface == null) return null;

        byte[] req = new byte[8];
        req[0] = 0x02;

        byte[] resp = HidQuery.Run(iface, req, 3, 400, 120,
            new Func<byte[], bool>(got => got != null && got.Length >= 4 && got[0] == 0x02));

        if (resp == null) return null;

        BatteryReading r = new BatteryReading();
        r.Protocol = Name;
        r.RawPercent = resp[2];
        r.Percent = Clamp(resp[2]);
        r.Charge = resp[3] == 0x01 ? ChargeState.Charging : ChargeState.Discharging;
        r.Ok = true;
        return r;
    }
}
```

然后把它注册进 `ProtocolRegistry.All()`。

几条要紧的规矩：

- **`Matches` 必须便宜且无副作用。** 每次扫描都会对每一个连着的设备组跑一遍。
- **没回包就返回 `null`**，不要返回一个 `Ok = false` 的读数。返回 `null` 才能让下一个协议接手。
- **填 `Protocol = Name`。** 它会进缓存文件，出问题时好判断是谁读的。
- **`RawPercent` 和 `Percent` 都要填。** 过滤器要原始值；`Percent` 会被扫描器覆盖成过滤后的值。
- **别在 `Probe` 里悄悄钳位。** `Clamp` 是给真正需要换算越界的情况用的；一个本该 0–100 的字节读出 116，通常说明你匹配错了帧 —— 扫描器宁愿丢掉它，也不想显示成 100。

如果设备报多块电池（带充电盒的耳塞），除了 `Percent` 还要填 `LeftPercent` / `RightPercent` / `CasePercent`。浮窗会把它们都显示出来。

### 为什么每次尝试都要重新开句柄

`HidQuery.Run` **每次尝试都重新开一个句柄**，并在重试前关掉它。这是故意的：一个超时的重叠读取会一直挂在句柄上，把下一次的回包吞掉，所以在同一个句柄上重试永远失败。关句柄才能取消挂起的 I/O。

---

## 5. 验证

```bat
build.bat
Volt.exe
```

查四件事：

1. 百分比和厂商软件显示的读数一致。
2. 拔掉数据线 —— 那一行应该切换连接方式，而不是多出一行。
3. 让设备闲置几分钟 —— 那一行应该显示 `上次 HH:mm`，而不是变空。
4. 在 `docs/DEVICES.md` 里如实标注 —— 只有真的在硬件上跑过才写 `Verified`。那个文件是生成的：把 key 加进 `tools/DevicesDoc.cs` 顶部的 `Verified` 数组，然后跑 `tools\gen-devices.bat`。

如果名字对不上，多半是匹配太松了。优先用 VID/PID 加报文长度，别用产品字符串；同一型号的不同接收器，产品字符串可能不一样，而 `2.4G Wireless Receiver` 这种通用名是整个厂商共用的。
