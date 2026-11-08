using System;
using System.Collections.Generic;

namespace Volt
{
    // =====================================================================
    // 各厂商私有协议。
    //
    // 每一套协议都来自公开的开源实现，不是猜的：
    //   Compx 17 字节  — Fan4Metal/mouse_tray nordic52.py、Fan4Metal/ATK_tray
    //   Compx 64 字节  — Fan4Metal/mouse_tray nordic54.py
    //   AULA 20 字节   — brhnshn/AulaTray DongleTransport.cs、Makkksx/AulaMonitor
    //   Razer          — openrazer（设备表）+ XAVIER0229/GearPower（帧格式）
    //   Logitech       — Solaar / HID++ 2.0 公开规范
    //   其余各家      — XAVIER0229/GearPower Protocols.cs 与 mouse_tray 驱动
    // =====================================================================

    /// <summary>
    /// Compx 17 字节帧（Report ID 0x08，命令 0x04）。
    /// ATK / VXE / VGN / 达尔优 / G-Wolves 这一大批国产 2.4G 外设共用。
    ///
    /// 请求：08 04 + 14 个 0 + 0x49
    /// 应答：08 04 .. [6]=电量 [7]=有线/充电 [8..9]=电压大端 mV，整帧 17 字节和 = 0x55
    /// </summary>
    public sealed class Compx17Protocol : BatteryProtocol
    {
        private static readonly ushort[] VendorIds = new ushort[]
        { 0x373B, 0x3554, 0x260D, 0x33E4, 0x1D57 };

        public override string Name { get { return "Compx 2.4G"; } }
        public override int Priority { get { return 30; } }

        public override bool Matches(HidGroup group)
        {
            if (!Contains(VendorIds, group.Vid)) return false;
            for (int i = 0; i < group.Interfaces.Count; i++)
            {
                HidInterface x = group.Interfaces[i];
                if (x.UsagePage == 0xFF02 && x.Usage == 0x0002 && x.OutputLength == 17) return true;
            }
            return false;
        }

        public override BatteryReading Probe(HidGroup group)
        {
            HidInterface iface = group.ByUsage(0xFF02, 0x0002, 17);
            if (iface == null) return null;

            byte[] req = new byte[17];
            req[0] = 0x08;
            req[1] = 0x04;
            req[16] = 0x49;

            byte[] resp = HidQuery.Run(iface, req, 4, 400, 120, IsReply);
            if (resp == null) return null;

            BatteryReading r = new BatteryReading();
            r.RawPercent = resp[6];
            r.Percent = resp[6];
            r.Charge = resp[7] == 0 ? ChargeState.Discharging : ChargeState.Charging;
            int mv = (resp[8] << 8) | resp[9];
            if (mv > 2000 && mv < 5000) r.VoltageMv = mv;
            if (r.Percent >= 100 && r.Charge == ChargeState.Charging) r.Charge = ChargeState.Full;
            r.Protocol = Name;
            r.Ok = true;
            return r;
        }

        /// <summary>
        /// 光看前两字节不够：同一个接口上还会混进别的报告，
        /// 把它们的第 6 字节当电量就会读出 116、123 这种值。
        /// 整帧 17 字节的和必须等于 0x55，这一条能挡掉所有杂帧。
        /// </summary>
        private static bool IsReply(byte[] got)
        {
            if (got == null || got.Length < 17) return false;
            if (got[0] != 0x08 || got[1] != 0x04) return false;
            int sum = 0;
            for (int i = 0; i < 17; i++) sum += got[i];
            return (sum & 0xFF) == 0x55;
        }

        internal static bool Contains(ushort[] set, ushort value)
        {
            for (int i = 0; i < set.Length; i++) if (set[i] == value) return true;
            return false;
        }
    }

    /// <summary>
    /// Compx 64 字节帧（Nordic 54L15，8K 接收器）。
    /// 目前已知用于 ATK Zero。
    ///
    /// 请求：08 7D 72 02 00 01 07 01 + 56 个 0（无线）／ 08 7C 72 02 00 00 07 01 + 0（有线）
    /// 应答：[1]=0x72 [5]=0x07 [2]=0x00 表示设备醒了，电量在 [7]
    /// </summary>
    public sealed class Compx64Protocol : BatteryProtocol
    {
        private static readonly ushort[] VendorIds = new ushort[]
        { 0x373B, 0x3554, 0x260D, 0x33E4 };

        public override string Name { get { return "Compx 8K"; } }
        public override int Priority { get { return 25; } }

        public override bool Matches(HidGroup group)
        {
            if (!Compx17Protocol.Contains(VendorIds, group.Vid)) return false;
            for (int i = 0; i < group.Interfaces.Count; i++)
            {
                HidInterface x = group.Interfaces[i];
                if (x.UsagePage == 0xFF05 && x.Usage == 0x0001 && x.OutputLength >= 64) return true;
            }
            return false;
        }

        public override BatteryReading Probe(HidGroup group)
        {
            HidInterface iface = group.ByUsage(0xFF05, 0x0001);
            if (iface == null || iface.OutputLength < 64) return null;

            // 先按无线问，再按有线问。两种链路的命令字节不一样。
            byte[] wireless = Build(iface.OutputLength, 0x7D, 0x01);
            byte[] resp = HidQuery.Run(iface, wireless, 3, 500, 150, IsReply);
            bool wired = false;
            if (resp == null)
            {
                byte[] cable = Build(iface.OutputLength, 0x7C, 0x00);
                resp = HidQuery.Run(iface, cable, 2, 500, 150, IsReply);
                wired = resp != null;
            }
            if (resp == null) return null;

            BatteryReading r = new BatteryReading();
            r.RawPercent = resp[7];
            r.Percent = resp[7];
            r.Charge = wired ? ChargeState.Charging : ChargeState.Discharging;
            if (r.Percent >= 100 && wired) r.Charge = ChargeState.Full;
            r.Protocol = Name;
            r.Ok = true;
            return r;
        }

        private static byte[] Build(int length, byte command, byte link)
        {
            byte[] req = new byte[length];
            req[0] = 0x08;
            req[1] = command;
            req[2] = 0x72;
            req[3] = 0x02;
            req[5] = link;
            req[6] = 0x07;
            req[7] = 0x01;
            return req;
        }

        private static bool IsReply(byte[] got)
        {
            if (got == null || got.Length < 9) return false;
            return got[1] == 0x72 && got[5] == 0x07 && got[2] == 0x00;
        }
    }

    /// <summary>
    /// 狼蛛 / AULA 20 字节帧（Report ID 0x13）。
    /// 请求：13 CA + 17 个 0，末字节 = sum8(前 19 字节)。
    ///
    /// 接收器会回两种格式，两种都得认，否则其中一种会被当成没应答：
    ///   13 4A 01 00 02 &lt;电量&gt; &lt;状态&gt; ...   电量在 [5]
    ///   13 0A 01 00 04 05 &lt;电量&gt; &lt;状态&gt; ...  电量在 [6]
    /// 状态 0x10 = 充电中，0x01 = 未充电。
    /// </summary>
    public sealed class AulaProtocol : BatteryProtocol
    {
        private static readonly byte[] Prefix4A = new byte[] { 0x13, 0x4A, 0x01, 0x00, 0x02 };
        private static readonly byte[] Prefix0A = new byte[] { 0x13, 0x0A, 0x01, 0x00, 0x04, 0x05 };

        public override string Name { get { return "AULA 2.4G"; } }
        public override int Priority { get { return 20; } }

        public override bool Matches(HidGroup group)
        {
            if (group.Vid != 0x3554 && group.Vid != 0x258A) return false;
            for (int i = 0; i < group.Interfaces.Count; i++)
            {
                HidInterface x = group.Interfaces[i];
                if (x.UsagePage == 0xFF02 && x.Usage == 0x0002 && x.OutputLength == 20) return true;
            }
            return false;
        }

        public override BatteryReading Probe(HidGroup group)
        {
            HidInterface iface = group.ByUsage(0xFF02, 0x0002, 20);
            if (iface == null) return null;

            byte[] req = new byte[20];
            req[0] = 0x13;
            req[1] = 0xCA;
            int sum = 0;
            for (int i = 0; i < 19; i++) sum += req[i];
            req[19] = (byte)(sum & 0xFF);

            byte[] resp = HidQuery.Run(iface, req, 4, 600, 800, IsReply);
            if (resp == null) return null;

            int pctOffset;
            int statusOffset;
            if (HidQuery.StartsWith(resp, Prefix4A)) { pctOffset = 5; statusOffset = 6; }
            else { pctOffset = 6; statusOffset = 7; }

            BatteryReading r = new BatteryReading();
            r.RawPercent = resp[pctOffset];
            r.Percent = resp[pctOffset];
            int status = resp[statusOffset];
            if (status == 0x10) r.Charge = ChargeState.Charging;
            else if (status == 0x01) r.Charge = ChargeState.Discharging;
            else r.Charge = ChargeState.Unknown;
            if (r.Percent >= 100 && r.Charge == ChargeState.Charging) r.Charge = ChargeState.Full;
            r.Protocol = Name;
            r.Ok = true;
            return r;
        }

        private static bool IsReply(byte[] resp)
        {
            return HidQuery.StartsWith(resp, Prefix4A) || HidQuery.StartsWith(resp, Prefix0A);
        }
    }

    /// <summary>
    /// Razer 鼠标：91 字节 Feature 报告，命令类 0x02 / 命令 0x07，
    /// 校验是 [3..8] 的异或放在 [89]，电量是原始字节 [10] 按 255 折算。
    ///
    /// Feature 报告在「零权限句柄」上也能读，所以 Windows 独占鼠标集合时照样能问。
    /// </summary>
    public sealed class RazerMouseProtocol : BatteryProtocol
    {
        private static readonly byte[] Transactions = new byte[] { 0x1F, 0x3F, 0xFF };

        public override string Name { get { return "Razer HID"; } }
        public override int Priority { get { return 40; } }

        public override bool Matches(HidGroup group)
        {
            if (group.Vid != 0x1532) return false;
            for (int i = 0; i < group.Interfaces.Count; i++)
                if (group.Interfaces[i].FeatureLength >= 91) return true;
            return false;
        }

        public override BatteryReading Probe(HidGroup group)
        {
            HidInterface iface = group.ByFeature(91);
            if (iface == null) return null;

            for (int t = 0; t < Transactions.Length; t++)
            {
                byte tx = Transactions[t];
                int length = iface.FeatureLength > 91 ? iface.FeatureLength : 91;
                byte[] req = new byte[length];
                req[0] = 0x00;
                req[2] = tx;
                req[6] = 0x02;
                req[7] = 0x07;
                req[8] = 0x80;
                byte crc = 0;
                for (int i = 3; i <= 8; i++) crc ^= req[i];
                req[89] = crc;
                req[90] = 0x00;

                byte[] resp = HidQuery.RunFeature(iface, req, 340, 2, 120, IsReply);
                if (resp == null) continue;

                BatteryReading r = new BatteryReading();
                r.RawPercent = resp[10];
                r.Percent = Clamp((int)Math.Round(resp[10] / 255.0 * 100.0));
                // 这条查询不带充电位。原始字节为 0 说明设备在休眠。
                r.Charge = ChargeState.Discharging;
                r.Asleep = resp[10] == 0;
                r.Protocol = Name;
                r.Note = "TX " + tx.ToString("X2");
                r.Ok = true;
                return r;
            }
            return null;
        }

        private static bool IsReply(byte[] resp)
        {
            return resp != null && resp.Length > 10 && resp[1] == 0x02;
        }
    }

    /// <summary>
    /// Razer 耳机（BlackShark V2 Pro 2.4 等）：64 字节 Output 报告，
    /// 电池在应答 [15]（少数型号在 [14]）。
    /// </summary>
    public sealed class RazerHeadsetProtocol : BatteryProtocol
    {
        public override string Name { get { return "Razer 2.4G 耳机"; } }
        public override int Priority { get { return 41; } }

        public override bool Matches(HidGroup group)
        {
            if (group.Vid != 0x1532) return false;
            for (int i = 0; i < group.Interfaces.Count; i++)
            {
                HidInterface x = group.Interfaces[i];
                if (x.UsagePage == 0xFF00 && x.OutputLength >= 64 && x.InputLength >= 64) return true;
            }
            return false;
        }

        public override BatteryReading Probe(HidGroup group)
        {
            HidInterface iface = group.ByUsage(0xFF00, 0x0002);
            if (iface == null) iface = group.ByOutput(64);
            if (iface == null || iface.OutputLength < 64) return null;

            byte[] req = new byte[64];
            req[0] = 0x02;
            req[1] = 0x80;
            req[2] = 0x08;
            req[5] = 0x50;
            req[6] = 0x41;
            req[7] = 0x08;
            req[9] = 0x03;
            req[10] = 0x21;

            byte[] resp = HidQuery.Run(iface, req, 3, 700, 150, IsReply);
            if (resp == null) return null;

            BatteryReading r = new BatteryReading();
            int pct = resp[0] == 0x02 ? resp[15] : resp[14];
            r.RawPercent = pct;
            r.Percent = Clamp(pct);
            r.Charge = ChargeState.Discharging;
            r.Protocol = Name;
            r.Ok = true;
            return r;
        }

        private static bool IsReply(byte[] resp)
        {
            if (resp == null || resp.Length < 16) return false;
            int pct = resp[0] == 0x02 ? resp[15] : resp[14];
            return pct >= 1 && pct <= 100;
        }
    }

    /// <summary>
    /// 华硕 ROG：6 字节 Feature 报告，[1]=0x12 [2]=0x07，
    /// 电量在 [5]，充电位在 [10]。
    /// </summary>
    public sealed class AsusProtocol : BatteryProtocol
    {
        private static readonly byte[] ReportIds = new byte[] { 0x00, 0x02, 0x05 };

        public override string Name { get { return "ASUS ROG"; } }
        public override int Priority { get { return 45; } }

        public override bool Matches(HidGroup group)
        {
            if (group.Vid != 0x0B05) return false;
            for (int i = 0; i < group.Interfaces.Count; i++)
                if (group.Interfaces[i].FeatureLength >= 6) return true;
            return false;
        }

        public override BatteryReading Probe(HidGroup group)
        {
            HidInterface iface = group.ByFeature(6);
            if (iface == null) return null;

            for (int id = 0; id < ReportIds.Length; id++)
            {
                int length = iface.FeatureLength > 6 ? iface.FeatureLength : 6;
                byte[] req = new byte[length];
                req[0] = ReportIds[id];
                req[1] = 0x12;
                req[2] = 0x07;

                byte[] resp = HidQuery.RunFeature(iface, req, 140, 2, 100, IsReply);
                if (resp == null) continue;

                BatteryReading r = new BatteryReading();
                r.RawPercent = resp[5];
                r.Percent = Clamp(resp[5]);
                bool charging = resp.Length > 10 && resp[10] != 0;
                r.Charge = charging ? (r.Percent >= 100 ? ChargeState.Full : ChargeState.Charging)
                                    : ChargeState.Discharging;
                r.Protocol = Name;
                r.Ok = true;
                return r;
            }
            return null;
        }

        private static bool IsReply(byte[] resp)
        {
            return resp != null && resp.Length > 5 && resp[1] == 0x12 && resp[2] == 0x07
                && resp[5] >= 1 && resp[5] <= 100;
        }
    }

    /// <summary>
    /// 兰族 Lamzu：65 字节 Feature 报告，[3]=0x02 [4]=0x02 [6]=0x83，
    /// 应答 [1]=0xA1 [6]=0x83，充电在 [7]，电量在 [8]。
    /// </summary>
    public sealed class LamzuProtocol : BatteryProtocol
    {
        public override string Name { get { return "Lamzu"; } }
        public override int Priority { get { return 44; } }

        public override bool Matches(HidGroup group)
        {
            if (group.Vid != 0x373E && group.Vid != 0x37B0) return false;
            for (int i = 0; i < group.Interfaces.Count; i++)
                if (group.Interfaces[i].FeatureLength >= 9) return true;
            return false;
        }

        public override BatteryReading Probe(HidGroup group)
        {
            HidInterface iface = group.ByFeature(9);
            if (iface == null) return null;

            int length = iface.FeatureLength > 65 ? iface.FeatureLength : 65;
            byte[] req = new byte[length];
            req[3] = 0x02;
            req[4] = 0x02;
            req[6] = 0x83;

            byte[] resp = HidQuery.RunFeature(iface, req, 140, 3, 100, IsReply);
            if (resp == null) return null;

            BatteryReading r = new BatteryReading();
            r.RawPercent = resp[8];
            r.Percent = Clamp(resp[8]);
            r.Charge = resp[7] == 0x01
                ? (r.Percent >= 100 ? ChargeState.Full : ChargeState.Charging)
                : ChargeState.Discharging;
            r.Protocol = Name;
            r.Ok = true;
            return r;
        }

        private static bool IsReply(byte[] resp)
        {
            return resp != null && resp.Length > 8 && resp[1] == 0xA1 && resp[6] == 0x83
                && resp[8] >= 1 && resp[8] <= 100;
        }
    }

    /// <summary>
    /// 罗技 HID++ 2.0。这是覆盖面最广的一套：一个接收器上配对的所有设备
    /// 都走同一条通道，型号在运行时读出来，不用逐条列举。
    ///
    /// 流程：先在 1..6 号设备索引里找到会应答的那个，再问
    /// 0x1004 统一电池 / 0x1000 电池状态 / 0x1001 电池电压，按顺序试。
    /// </summary>
    public sealed class LogitechProtocol : BatteryProtocol
    {
        private const byte ShortReport = 0x10;
        private const byte LongReport = 0x11;
        private const byte SoftwareId = 0x01;
        private const byte RootFeature = 0x00;

        private const ushort UnifiedBattery = 0x1004;
        private const ushort BatteryStatus = 0x1000;
        private const ushort BatteryVoltage = 0x1001;
        private const ushort DeviceName = 0x0005;
        private const ushort DeviceFriendlyName = 0x0007;

        public override string Name { get { return "Logitech HID++"; } }
        public override int Priority { get { return 50; } }

        public override bool Matches(HidGroup group)
        {
            if (group.Vid != 0x046D) return false;
            for (int i = 0; i < group.Interfaces.Count; i++)
                if (group.Interfaces[i].UsagePage == 0xFF00) return true;
            return false;
        }

        public override BatteryReading Probe(HidGroup group)
        {
            HidInterface iface = null;
            for (int i = 0; i < group.Interfaces.Count; i++)
            {
                HidInterface x = group.Interfaces[i];
                if (x.UsagePage == 0xFF00 && x.OutputLength >= 7)
                {
                    if (iface == null || x.OutputLength < iface.OutputLength) iface = x;
                }
            }
            if (iface == null) return null;

            HidNative.Handle h = HidNative.Handle.Open(iface.Path);
            if (h == null || !h.IsOpen)
            {
                if (h != null) h.Dispose();
                return null;
            }

            try
            {
                byte dev = ResolveDeviceIndex(h, iface);
                if (dev == 0)
                {
                    // 接收器在，配对的设备没醒。界面显示上次读数，不当成没设备。
                    BatteryReading asleep = new BatteryReading();
                    asleep.Asleep = true;
                    asleep.Note = "接收器在线 · 设备休眠或未唤醒";
                    asleep.Protocol = Name;
                    return asleep;
                }

                System.Threading.Thread.Sleep(260);

                BatteryReading r = ReadFeature(h, iface, dev, UnifiedBattery, 0x01, true);
                if (r != null) return r;
                r = ReadFeature(h, iface, dev, BatteryStatus, 0x00, true);
                if (r != null) return r;
                r = ReadFeature(h, iface, dev, BatteryVoltage, 0x00, false);
                if (r != null) return r;
                return null;
            }
            finally
            {
                h.Dispose();
            }
        }

        private BatteryReading ReadFeature(HidNative.Handle h, HidInterface iface, byte dev,
            ushort feature, byte function, bool percentMode)
        {
            int featureIndex = FeatureIndex(h, iface, dev, feature);
            if (featureIndex <= 0) return null;

            byte[] resp = Call(h, iface, dev, (byte)featureIndex, function, 0, 0, 0);
            if (resp == null || resp.Length < 7) return null;

            BatteryReading r = new BatteryReading();
            if (percentMode)
            {
                int percent = resp[4];
                if (percent < 0 || percent > 100) return null;
                int status = resp[6];
                r.RawPercent = percent;
                r.Percent = percent;
                if (status == 3) r.Charge = ChargeState.Full;
                else if (status == 1 || status == 2 || status == 4) r.Charge = ChargeState.Charging;
                else r.Charge = ChargeState.Discharging;
                if (r.Percent >= 100 && r.Charge == ChargeState.Charging) r.Charge = ChargeState.Full;
            }
            else
            {
                int mv = (resp[4] << 8) | resp[5];
                int percent = VoltageToPercent(mv);
                if (percent < 0) return null;
                int flags = resp[6];
                r.VoltageMv = mv;
                r.RawPercent = percent;
                r.Percent = percent;
                bool charging = (flags & 0x80) != 0;
                r.Charge = charging ? (percent >= 100 ? ChargeState.Full : ChargeState.Charging)
                                    : ChargeState.Discharging;
            }

            r.ModelName = ReadDeviceName(h, iface, dev);
            r.Protocol = Name;
            r.Ok = true;
            return r;
        }

        /// <summary>
        /// 罗技的电池电压是 100 级查表，不是线性换算。这张表取自
        /// Linux 内核 hid-logitech-hidpp.c 的 hidpp20_map_battery_capacity()。
        /// </summary>
        private static int VoltageToPercent(int millivolts)
        {
            int[,] curve = new int[,]
            {
                { 4186, 100 }, { 4067, 90 }, { 3989, 80 }, { 3922, 70 },
                { 3859, 60 }, { 3811, 50 }, { 3778, 40 }, { 3751, 30 },
                { 3717, 20 }, { 3671, 10 }, { 3646, 5 }, { 3579, 2 }, { 3500, 0 }
            };
            if (millivolts >= curve[0, 0]) return curve[0, 1];
            if (millivolts <= curve[curve.GetLength(0) - 1, 0]) return curve[curve.GetLength(0) - 1, 1];
            for (int i = 0; i < curve.GetLength(0) - 1; i++)
            {
                int vHigh = curve[i, 0];
                int pHigh = curve[i, 1];
                int vLow = curve[i + 1, 0];
                int pLow = curve[i + 1, 1];
                if (millivolts <= vHigh && millivolts >= vLow)
                {
                    double value = pLow + (pHigh - pLow) * (millivolts - vLow) / (double)(vHigh - vLow);
                    return (int)Math.Round(value);
                }
            }
            return -1;
        }

        private static byte ResolveDeviceIndex(HidNative.Handle h, HidInterface iface)
        {
            for (byte dev = 1; dev <= 6; dev++)
            {
                if (FeatureIndex(h, iface, dev, UnifiedBattery) > 0) return dev;
                if (FeatureIndex(h, iface, dev, BatteryStatus) > 0) return dev;
            }
            if (FeatureIndex(h, iface, 0xFF, UnifiedBattery) > 0) return 0xFF;
            if (FeatureIndex(h, iface, 0xFF, BatteryStatus) > 0) return 0xFF;
            return 0;
        }

        private static int FeatureIndex(HidNative.Handle h, HidInterface iface, byte dev, ushort feature)
        {
            byte[] resp = Call(h, iface, dev, RootFeature, 0x00,
                (byte)(feature >> 8), (byte)(feature & 0xFF), 0);
            if (resp == null || resp.Length < 6) return -1;
            if (resp[4] == 0x00 && resp[5] == 0x00) return -1;
            return resp[4];
        }

        private static string ReadDeviceName(HidNative.Handle h, HidInterface iface, byte dev)
        {
            ushort[] features = new ushort[] { DeviceName, DeviceFriendlyName };
            for (int f = 0; f < features.Length; f++)
            {
                int featureIndex = FeatureIndex(h, iface, dev, features[f]);
                if (featureIndex <= 0) continue;

                byte[] lengthReply = Call(h, iface, dev, (byte)featureIndex, 0x00, 0, 0, 0);
                if (lengthReply == null || lengthReply.Length < 5) continue;
                int nameLength = lengthReply[4];
                if (nameLength <= 0 || nameLength > 64) continue;

                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                for (int offset = 0; offset < nameLength; offset += 16)
                {
                    byte[] chunk = Call(h, iface, dev, (byte)featureIndex, 0x10, (byte)offset, 0, 0);
                    if (chunk == null || chunk.Length < 5) break;
                    for (int i = 4; i < chunk.Length && sb.Length < nameLength; i++)
                    {
                        if (chunk[i] == 0) break;
                        sb.Append((char)chunk[i]);
                    }
                }
                string name = sb.ToString().Trim();
                if (name.Length > 0) return name;
            }
            return "";
        }

        /// <summary>
        /// 发一条 HID++ 请求并读回应答。报告长度由接口自己的 OutputLength 决定，
        /// 短报告 7 字节、长报告 20 字节。
        /// </summary>
        private static byte[] Call(HidNative.Handle h, HidInterface iface, byte dev,
            byte featureIndex, byte function, byte p0, byte p1, byte p2)
        {
            int length = iface.OutputLength;
            if (length < 7) length = 7;
            if (length > 20) length = 20;

            byte[] req = new byte[length];
            req[0] = length > 7 ? LongReport : ShortReport;
            req[1] = dev;
            req[2] = featureIndex;
            req[3] = (byte)((function << 4) | SoftwareId);
            if (length > 4) req[4] = p0;
            if (length > 5) req[5] = p1;
            if (length > 6) req[6] = p2;

            for (int attempt = 0; attempt < 3; attempt++)
            {
                byte[] got = h.Transact(req, iface.InputLength, 320);
                if (got != null && got.Length >= 4)
                {
                    // 应答必须来自同一个设备索引和同一个特性，否则是别人插话
                    if (got[1] == dev && got[2] == featureIndex) return got;
                }
                System.Threading.Thread.Sleep(80);
            }
            return null;
        }
    }

    /// <summary>
    /// MCHOSE / Realtek 方案：0xFF01 页，请求全填 0xFF 后异或，
    /// 应答是 0x13 或 0xE2 开头的推送报告，整段与 0xFF 异或解码。
    /// </summary>
    public sealed class MchoseProtocol : BatteryProtocol
    {
        private static readonly ushort[] VendorIds = new ushort[] { 0x3837, 0x5253 };

        public override string Name { get { return "MCHOSE"; } }
        public override int Priority { get { return 42; } }

        public override bool Matches(HidGroup group)
        {
            if (!Compx17Protocol.Contains(VendorIds, group.Vid)) return false;
            for (int i = 0; i < group.Interfaces.Count; i++)
                if (group.Interfaces[i].UsagePage == 0xFF01) return true;
            return false;
        }

        public override BatteryReading Probe(HidGroup group)
        {
            HidInterface iface = null;
            for (int i = 0; i < group.Interfaces.Count; i++)
                if (group.Interfaces[i].UsagePage == 0xFF01) { iface = group.Interfaces[i]; break; }
            if (iface == null || iface.OutputLength < 3) return null;

            int length = iface.OutputLength;
            if (length < 3) length = 3;
            if (length > 21) length = 21;

            byte[] req = new byte[length];
            for (int i = 0; i < length; i++) req[i] = 0xFF;
            req[1] = 0x0B ^ 0xFF;
            req[2] = 0xAA ^ 0xFF;

            byte[] resp = HidQuery.Run(iface, req, 4, 700, 200, IsReply);
            if (resp == null) return null;

            int offset = resp[0] == 0x13 ? 1 : (resp[0] == 0xE2 ? 0 : -1);
            if (offset < 0 || resp.Length < offset + 5) return null;

            byte[] data = new byte[resp.Length - offset];
            for (int i = 0; i < data.Length; i++) data[i] = (byte)(resp[offset + i] ^ 0xFF);
            if (data.Length < 5 || data[0] != 0xE2) return null;

            BatteryReading r = new BatteryReading();
            r.RawPercent = data[4];
            r.Percent = Clamp(data[4]);
            r.Charge = data[3] != 0
                ? (r.Percent >= 100 ? ChargeState.Full : ChargeState.Charging)
                : ChargeState.Discharging;
            r.Protocol = Name;
            r.Ok = true;
            return r;
        }

        private static bool IsReply(byte[] resp)
        {
            if (resp == null || resp.Length < 6) return false;
            if (resp[0] != 0x13 && resp[0] != 0xE2) return false;
            int offset = resp[0] == 0x13 ? 1 : 0;
            if (resp.Length < offset + 5) return false;
            return (byte)(resp[offset] ^ 0xFF) == 0xE2;
        }
    }

    /// <summary>
    /// HyperX Cloud Flight 系列：20 字节请求 21 FF 05，
    /// 应答 [0]=0x21 [1]=0xFF [2]=0x05，[3]=状态 [4]=电量档位。
    /// </summary>
    public sealed class HyperXProtocol : BatteryProtocol
    {
        private static readonly ushort[] VendorIds = new ushort[] { 0x03F0, 0x0951 };

        public override string Name { get { return "HyperX 2.4G"; } }
        public override int Priority { get { return 46; } }

        public override bool Matches(HidGroup group)
        {
            if (!Compx17Protocol.Contains(VendorIds, group.Vid)) return false;
            for (int i = 0; i < group.Interfaces.Count; i++)
            {
                HidInterface x = group.Interfaces[i];
                if (x.OutputLength >= 5 && x.OutputLength <= 32) return true;
            }
            return false;
        }

        public override BatteryReading Probe(HidGroup group)
        {
            HidInterface iface = group.ByOutput(20);
            if (iface == null) return null;

            int length = iface.OutputLength;
            byte[] req = new byte[length];
            req[0] = 0x21;
            req[1] = 0xFF;
            req[2] = 0x05;

            byte[] resp = HidQuery.Run(iface, req, 3, 700, 200, IsReply);
            if (resp == null) return null;

            BatteryReading r = new BatteryReading();
            int state = resp[3];
            int gauge = resp[4] != 0 ? resp[4] : state;
            // 电量档位是 1..10，乘 10 才是百分比
            int percent = gauge <= 10 ? gauge * 10 : gauge;
            r.RawPercent = percent;
            r.Percent = Clamp(percent);
            r.Charge = state == 0x10 ? ChargeState.Charging : ChargeState.Discharging;
            if (r.Percent >= 100 && r.Charge == ChargeState.Charging) r.Charge = ChargeState.Full;
            r.Protocol = Name;
            r.Ok = true;
            return r;
        }

        private static bool IsReply(byte[] resp)
        {
            if (resp == null || resp.Length < 5) return false;
            if (resp[0] != 0x21 || resp[1] != 0xFF || resp[2] != 0x05) return false;
            int gauge = resp[4] != 0 ? resp[4] : resp[3];
            return gauge >= 1 && gauge <= 100;
        }
    }

    /// <summary>
    /// Ninjutso：32 字节 Feature 报告（ID 5），
    /// 应答 [9]=电量 [10]=充电 [11]=充满 [12]=在线。
    /// </summary>
    public sealed class NinjutsoProtocol : BatteryProtocol
    {
        public override string Name { get { return "Ninjutso"; } }
        public override int Priority { get { return 43; } }

        public override bool Matches(HidGroup group)
        {
            if (group.Vid != 0x1915) return false;
            for (int i = 0; i < group.Interfaces.Count; i++)
                if (group.Interfaces[i].FeatureLength >= 32) return true;
            return false;
        }

        public override BatteryReading Probe(HidGroup group)
        {
            HidInterface iface = group.ByFeature(32);
            if (iface == null) return null;

            int length = iface.FeatureLength;
            byte[] req = new byte[length];
            req[0] = 0x05;
            req[1] = 0x15;
            req[4] = 0x01;

            byte[] resp = HidQuery.RunFeature(iface, req, 90, 3, 100, IsReply);
            if (resp == null) return null;

            BatteryReading r = new BatteryReading();
            r.RawPercent = resp[9];
            r.Percent = Clamp(resp[9]);
            bool charging = resp[10] != 0;
            bool full = resp.Length > 11 && resp[11] != 0;
            bool online = resp.Length > 12 && resp[12] != 0;
            if (full) r.Charge = ChargeState.Full;
            else if (charging) r.Charge = ChargeState.Charging;
            else r.Charge = ChargeState.Discharging;
            r.Asleep = !online || resp[9] == 0;
            r.Protocol = Name;
            r.Ok = true;
            return r;
        }

        private static bool IsReply(byte[] resp)
        {
            return resp != null && resp.Length > 12 && resp[9] >= 1 && resp[9] <= 100;
        }
    }

    /// <summary>
    /// A4Tech Bloody 8K：先用 8 字节 Feature 命令催一次，
    /// 再从 0xFF01 通道读 8 字节 Input 报告，电池行是 06 56 PP CC。
    /// </summary>
    public sealed class BloodyProtocol : BatteryProtocol
    {
        public override string Name { get { return "Bloody 8K"; } }
        public override int Priority { get { return 47; } }

        public override bool Matches(HidGroup group)
        {
            return group.Vid == 0x1956;
        }

        public override BatteryReading Probe(HidGroup group)
        {
            HidInterface status = group.ByUsage(0xFF01, 0x0001);
            if (status == null) status = group.ByUsage(0xFF01, 0x0000);
            if (status == null) return null;

            HidNative.Handle h = HidNative.Handle.Open(status.Path);
            if (h == null || !h.IsOpen)
            {
                if (h != null) h.Dispose();
                return null;
            }

            try
            {
                byte[] command = new byte[] { 0x80, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80 };
                h.SetFeature(command);

                for (int attempt = 0; attempt < 3; attempt++)
                {
                    byte[] resp = h.Read(status.InputLength, 600);
                    if (resp == null || resp.Length < 4) continue;
                    if (resp[0] != 0x06 || resp[1] != 0x56) continue;
                    // 255 表示无线链路断了，这一帧不算数
                    if (resp[2] == 0xFF) continue;

                    BatteryReading r = new BatteryReading();
                    r.RawPercent = resp[2];
                    r.Percent = Clamp(resp[2]);
                    bool wired = resp[3] != 0;
                    r.Charge = wired ? (r.Percent >= 100 ? ChargeState.Full : ChargeState.Charging)
                                     : ChargeState.Discharging;
                    r.Protocol = Name;
                    r.Ok = true;
                    return r;
                }
                return null;
            }
            finally
            {
                h.Dispose();
            }
        }
    }

    /// <summary>
    /// 攻击鲨 Attack Shark：设备自己往 0x000A 页推报告，
    /// 电量在 [4]，单位是 10%（所以乘 10）。
    /// </summary>
    public sealed class AttackSharkProtocol : BatteryProtocol
    {
        public override string Name { get { return "Attack Shark"; } }
        public override int Priority { get { return 48; } }

        public override bool Matches(HidGroup group)
        {
            if (group.Vid != 0x1D57) return false;
            for (int i = 0; i < group.Interfaces.Count; i++)
                if (group.Interfaces[i].UsagePage == 0x000A) return true;
            return false;
        }

        public override BatteryReading Probe(HidGroup group)
        {
            HidInterface iface = group.ByUsage(0x000A, 0x0000);
            if (iface == null)
            {
                for (int i = 0; i < group.Interfaces.Count; i++)
                    if (group.Interfaces[i].UsagePage == 0x000A) { iface = group.Interfaces[i]; break; }
            }
            if (iface == null) return null;

            byte[] resp = HidQuery.RunPush(iface, null, iface.InputLength, 2500, 2, 100, IsReply);
            if (resp == null) return null;

            BatteryReading r = new BatteryReading();
            int percent = resp[4] * 10;
            r.RawPercent = percent;
            r.Percent = Clamp(percent);
            r.Charge = r.Percent >= 100 ? ChargeState.Full : ChargeState.Discharging;
            r.Protocol = Name;
            r.Ok = true;
            return r;
        }

        private static bool IsReply(byte[] resp)
        {
            return resp != null && resp.Length > 4 && resp[4] >= 1 && resp[4] <= 10;
        }
    }

    /// <summary>
    /// 标准 HID 电池（Usage Page 0x06 / Usage 0x20）。
    /// 蓝牙外设通常直接暴露这一页，不需要任何私有协议。
    /// </summary>
    public sealed class StandardBatteryProtocol : BatteryProtocol
    {
        public override string Name { get { return "HID 标准电池"; } }
        public override int Priority { get { return 90; } }

        public override bool Matches(HidGroup group)
        {
            for (int i = 0; i < group.Interfaces.Count; i++)
            {
                HidInterface x = group.Interfaces[i];
                if (x.UsagePage == 0x0006 && x.Usage == 0x0020) return true;
            }
            return false;
        }

        public override BatteryReading Probe(HidGroup group)
        {
            HidInterface iface = group.ByUsage(0x0006, 0x0020);
            if (iface == null || iface.FeatureLength < 2) return null;

            HidNative.Handle h = HidNative.Handle.Open(iface.Path);
            if (h == null || !h.IsOpen)
            {
                if (h != null) h.Dispose();
                return null;
            }

            try
            {
                byte[] buffer = new byte[iface.FeatureLength];
                if (!h.GetFeature(buffer)) return null;
                int percent = buffer[1];
                if (percent < 0 || percent > 100) return null;

                BatteryReading r = new BatteryReading();
                r.RawPercent = percent;
                r.Percent = percent;
                r.Charge = ChargeState.Unknown;
                r.Protocol = Name;
                r.Ok = true;
                return r;
            }
            finally
            {
                h.Dispose();
            }
        }
    }

    /// <summary>
    /// Sony INZONE Buds（054C:0EC2）—— 目前开源世界里唯一一份真能拿到
    /// 左耳 / 右耳 / 充电盒三路电量的实现。
    ///
    /// 协议是纯推送式：不发任何命令，dongle 自己周期性吐 64 字节状态包，
    /// 找到 [1]==0x12 且 [2]==0x04 的那一包即可。
    /// [14]=右耳 [16]=左耳 [18]=盒，[19] = (右+左+117) mod 256 作校验。
    ///
    /// 上游 HeadsetControl 读到了三个字节，但最后 `level_percent = min(left, right)`
    /// 把盒电量丢掉了——因为它的结果结构只装得下一个数。Volt 这边三路都留下。
    /// </summary>
    public sealed class SonyInzoneBudsProtocol : BatteryProtocol
    {
        public override string Name { get { return "Sony INZONE"; } }
        public override int Priority { get { return 34; } }

        private const int RightByte = 14;
        private const int LeftByte = 16;
        private const int CaseByte = 18;

        public override bool Matches(HidGroup group)
        {
            return group.Vid == 0x054C && group.Pid == 0x0EC2;
        }

        public override BatteryReading Probe(HidGroup group)
        {
            HidInterface iface = group.ByOutput(64);
            if (iface == null) iface = group.ByUsage(0xFF00, 0x0001);
            if (iface == null)
            {
                for (int i = 0; i < group.Interfaces.Count; i++)
                    if (group.Interfaces[i].InputLength >= 32) { iface = group.Interfaces[i]; break; }
            }
            if (iface == null) return null;

            int readLength = iface.InputLength > 0 ? iface.InputLength : 64;
            byte[] resp = HidQuery.RunPush(iface, null, readLength, 700, 4, 120, IsBattery);
            if (resp == null) return null;

            BatteryReading r = new BatteryReading();
            r.LeftPercent = resp[LeftByte];
            r.RightPercent = resp[RightByte];
            r.CasePercent = resp[CaseByte];

            int worst = int.MaxValue;
            if (r.LeftPercent >= 1 && r.LeftPercent <= 100 && r.LeftPercent < worst) worst = r.LeftPercent;
            if (r.RightPercent >= 1 && r.RightPercent <= 100 && r.RightPercent < worst) worst = r.RightPercent;
            if (worst == int.MaxValue) return null;

            r.RawPercent = worst;
            r.Percent = Clamp(worst);
            // 这个包里没有充电位；两耳都满且盒不在充电时按放电处理。
            r.Charge = r.Percent >= 100 ? ChargeState.Full : ChargeState.Discharging;
            r.Protocol = Name;
            r.Ok = true;
            return r;
        }

        private static bool IsBattery(byte[] resp)
        {
            if (resp == null || resp.Length <= CaseByte) return false;
            if (resp[1] != 0x12 || resp[2] != 0x04) return false;
            int sum = (resp[RightByte] + resp[LeftByte] + 117) & 0xFF;
            if (resp.Length > 19 && resp[19] != sum) return false;
            return resp[LeftByte] >= 1 && resp[LeftByte] <= 100;
        }
    }

    /// <summary>
    /// Audeze Maxwell（Airoha 平台）。62 字节报文，写用报告 ID 0x06，读用 0x07。
    /// 请求：05 5A 05 00 D6 0C ...（0x5A=请求，0xD6 0x0C=电量命令）
    /// 应答：05 5D 05 00 D6 0C 00 00 &lt;percent&gt;，[8] 就是百分比，[6] 非 0 表示被拒。
    /// </summary>
    public sealed class AudezeProtocol : BatteryProtocol
    {
        public override string Name { get { return "Audeze"; } }
        public override int Priority { get { return 35; } }

        public override bool Matches(HidGroup group)
        {
            return group.Vid == 0x3329;
        }

        public override BatteryReading Probe(HidGroup group)
        {
            HidInterface iface = null;
            for (int i = 0; i < group.Interfaces.Count; i++)
            {
                HidInterface x = group.Interfaces[i];
                if (x.OutputLength >= 8 && x.InputLength >= 8) { iface = x; break; }
            }
            if (iface == null) return null;

            int length = iface.OutputLength > 0 ? iface.OutputLength : 62;
            byte[] req = new byte[length];
            req[0] = 0x06;
            req[1] = 0x05;   // MESSAGE_MARKER
            req[2] = 0x5A;   // TYPE_REQUEST
            req[3] = 0x05;
            req[4] = 0xD6;   // CMD_BATTERY[0]
            req[5] = 0x0C;   // CMD_BATTERY[1]

            int readLength = iface.InputLength > 0 ? iface.InputLength : 62;
            byte[] resp = HidQuery.RunPush(iface, req, readLength, 600, 3, 120, IsReply);
            if (resp == null) return null;

            BatteryReading r = new BatteryReading();
            r.RawPercent = resp[8];
            r.Percent = Clamp(resp[8]);
            r.Charge = r.Percent >= 100 ? ChargeState.Full : ChargeState.Discharging;
            r.Protocol = Name;
            r.Ok = true;
            return r;
        }

        private static bool IsReply(byte[] resp)
        {
            if (resp == null || resp.Length < 9) return false;
            if (resp[0] != 0x07 && resp[0] != 0x06) return false;
            int i = resp[0] == 0x07 ? 1 : 0;
            if (resp.Length < i + 8) return false;
            if (resp[i] != 0x05) return false;
            if (resp[i + 1] != 0x5D && resp[i + 1] != 0x5B) return false;   // RESULT / RESPONSE
            return resp[i + 6] == 0;   // STATUS_OFFSET：非 0 表示被拒
        }
    }

    /// <summary>
    /// Corsair Virtuoso XT / SE。厂商集合是 Usage Page 0xFF42 / Usage 0x0001，
    /// 64 字节报文：写报告 ID 0x02，回报告 ID 0x01。
    /// 请求 [1]=目标（0x09 接收器后面的耳机 / 0x08 本机）[2]=0x02 读 [3]=属性号。
    /// 属性 0x0F 是电量（单位千分之一，值/10 才是百分比），0x10 是充电状态（1=充电）。
    /// </summary>
    public sealed class CorsairVirtuosoProtocol : BatteryProtocol
    {
        public override string Name { get { return "Corsair Virtuoso"; } }
        public override int Priority { get { return 36; } }

        private const byte ReportOut = 0x02;
        private const byte ReportIn = 0x01;
        private const byte Get = 0x02;
        private const byte PropBatteryLevel = 0x0F;
        private const byte PropBatteryStatus = 0x10;

        public override bool Matches(HidGroup group)
        {
            if (group.Vid != 0x1B1C) return false;
            return group.ByUsage(0xFF42, 0x0001) != null;
        }

        public override BatteryReading Probe(HidGroup group)
        {
            HidInterface iface = group.ByUsage(0xFF42, 0x0001);
            if (iface == null) return null;

            // 有线 SE 拒绝 0x08 而应答 0x09，所以两个目标都试。
            byte[] targetList = new byte[] { 0x09, 0x08 };
            for (int t = 0; t < targetList.Length; t++)
            {
                int level = ReadProperty(iface, targetList[t], PropBatteryLevel);
                if (level < 0) continue;

                BatteryReading r = new BatteryReading();
                int percent = level / 10;
                r.RawPercent = percent;
                r.Percent = Clamp(percent);
                int status = ReadProperty(iface, targetList[t], PropBatteryStatus);
                r.Charge = status == 1
                    ? (r.Percent >= 100 ? ChargeState.Full : ChargeState.Charging)
                    : ChargeState.Discharging;
                r.Protocol = Name;
                r.Ok = true;
                return r;
            }
            return null;
        }

        private static int ReadProperty(HidInterface iface, byte target, byte property)
        {
            int length = iface.OutputLength > 0 ? iface.OutputLength : 64;
            byte[] req = new byte[length];
            req[0] = ReportOut;
            req[1] = target;
            req[2] = Get;
            req[3] = property;

            byte[] resp = HidQuery.RunPush(iface, req, length, 600, 2, 120,
                delegate(byte[] x) { return IsReply(x, target, property); });
            if (resp == null) return -1;

            int offset = resp[0] == ReportIn ? 1 : 0;
            if (resp.Length < offset + 8) return -1;
            int value = resp[offset + 3] | (resp[offset + 4] << 8) | (resp[offset + 5] << 16) | (resp[offset + 6] << 24);
            if (value < 0 || value > 1000) return -1;
            return value;
        }

        private static bool IsReply(byte[] resp, byte target, byte property)
        {
            if (resp == null || resp.Length < 8) return false;
            int offset = resp[0] == ReportIn ? 1 : 0;
            if (resp.Length < offset + 8) return false;
            if (resp[offset + 2] != Get) return false;
            if (resp[offset + 3] != 0x00) return false;   // STATUS_OK
            return true;
        }
    }

    /// <summary>
    /// Corsair Void 系列（含 Void V2）。不带报告 ID 的 2 字节命令：C9 64。
    /// 回 5 字节，[2] 是电量（bit7 是麦克风抬起标志，要抹掉），
    /// [4] 是状态：0 不可用、4/5 充电中、1/2 正常/低电。
    /// </summary>
    public sealed class CorsairVoidProtocol : BatteryProtocol
    {
        public override string Name { get { return "Corsair Void"; } }
        public override int Priority { get { return 37; } }

        public override bool Matches(HidGroup group)
        {
            if (group.Vid != 0x1B1C) return false;
            return group.ByUsage(0xFF42, 0x0001) == null;   // Virtuoso 走另一套，这里不抢
        }

        public override BatteryReading Probe(HidGroup group)
        {
            HidInterface iface = null;
            for (int i = 0; i < group.Interfaces.Count; i++)
            {
                HidInterface x = group.Interfaces[i];
                if (x.OutputLength >= 2 && x.InputLength >= 5) { iface = x; break; }
            }
            if (iface == null) return null;

            byte[] req = new byte[] { 0xC9, 0x64 };
            byte[] resp = HidQuery.Run(iface, req, 3, 600, 150, IsReply);
            if (resp == null) return null;

            BatteryReading r = new BatteryReading();
            int level = resp[2] & 0x7F;
            r.RawPercent = level;
            r.Percent = Clamp(level);
            int status = resp[4];
            if (status == 4 || status == 5)
                r.Charge = r.Percent >= 100 ? ChargeState.Full : ChargeState.Charging;
            else
                r.Charge = ChargeState.Discharging;
            r.Protocol = Name;
            r.Ok = true;
            return r;
        }

        private static bool IsReply(byte[] resp)
        {
            if (resp == null || resp.Length < 5) return false;
            int level = resp[2] & 0x7F;
            return level >= 1 && level <= 100;
        }
    }

    /// <summary>
    /// Jabra GNP（Evolve2 65 Flex / Link 380 / Link 390）。
    /// 64 字节报文，报告 ID 0x05：
    /// [1]=目标（0x04 经接收器 / 0x08 USB 直连）[2]=源 0x00 [3]=序号
    /// [4]=(类型&lt;&lt;6)|长度 [5]=命令 [6]=子命令 [7..]=负载。
    /// 读电量：类型 0x01（GET）、命令 0x12（STATUS）、子命令 0x02（耳机电量）。
    /// 应答类型 0x03，负载 [0] 的 bit0 是充电标志、[1] 是百分比。
    /// </summary>
    public sealed class JabraGnpProtocol : BatteryProtocol
    {
        public override string Name { get { return "Jabra"; } }
        public override int Priority { get { return 38; } }

        public override bool Matches(HidGroup group)
        {
            return group.Vid == 0x0B0E;
        }

        public override BatteryReading Probe(HidGroup group)
        {
            HidInterface iface = null;
            for (int i = 0; i < group.Interfaces.Count; i++)
            {
                HidInterface x = group.Interfaces[i];
                if (x.OutputLength >= 8 && x.InputLength >= 8) { iface = x; break; }
            }
            if (iface == null) return null;

            byte[] targets = new byte[] { 0x04, 0x08 };
            for (int t = 0; t < targets.Length; t++)
            {
                int length = iface.OutputLength > 0 ? iface.OutputLength : 64;
                byte[] req = new byte[length];
                req[0] = 0x05;
                req[1] = targets[t];
                req[2] = 0x00;
                req[3] = 0x01;                 // 序号，固定 1
                req[4] = (byte)((0x01 << 6) | 6);   // GET，负载 0 字节
                req[5] = 0x12;                 // CMD_STATUS
                req[6] = 0x02;                 // STATUS_HS_BATTERY

                byte[] resp = HidQuery.RunPush(iface, req, length, 600, 2, 120, IsReply);
                if (resp == null) continue;

                int offset = resp[0] == 0x05 ? 1 : 0;
                if (resp.Length < offset + 9) continue;

                BatteryReading r = new BatteryReading();
                int level = resp[offset + 8];
                r.RawPercent = level;
                r.Percent = Clamp(level);
                bool charging = (resp[offset + 7] & 0x01) != 0;
                r.Charge = charging
                    ? (r.Percent >= 100 ? ChargeState.Full : ChargeState.Charging)
                    : ChargeState.Discharging;
                r.Protocol = Name;
                r.Ok = true;
                return r;
            }
            return null;
        }

        private static bool IsReply(byte[] resp)
        {
            if (resp == null || resp.Length < 9) return false;
            int offset = resp[0] == 0x05 ? 1 : 0;
            if (resp.Length < offset + 9) return false;
            int type = resp[offset + 3] >> 6;
            if (type != 0x03) return false;   // TYPE_RESPONSE
            int level = resp[offset + 8];
            return level >= 1 && level <= 100;
        }
    }

    /// <summary>
    /// Lenovo Wireless VoIP（17EF:A07D）。61 字节 Feature 报文：
    /// [0]=0x24 命令头 [1]=0x01 状态命令，读回 [7] 就是百分比。
    /// </summary>
    public sealed class LenovoVoipProtocol : BatteryProtocol
    {
        public override string Name { get { return "Lenovo VoIP"; } }
        public override int Priority { get { return 39; } }

        public override bool Matches(HidGroup group)
        {
            return group.Vid == 0x17EF && group.Pid == 0xA07D;
        }

        public override BatteryReading Probe(HidGroup group)
        {
            HidInterface iface = group.ByFeature(8);
            if (iface == null) return null;

            int length = iface.FeatureLength;
            byte[] req = new byte[length];
            req[0] = 0x24;
            req[1] = 0x01;

            byte[] resp = HidQuery.RunFeature(iface, req, 100, 3, 120, IsReply);
            if (resp == null) return null;

            BatteryReading r = new BatteryReading();
            r.RawPercent = resp[7];
            r.Percent = Clamp(resp[7]);
            r.Charge = r.Percent >= 100 ? ChargeState.Full : ChargeState.Discharging;
            r.Protocol = Name;
            r.Ok = true;
            return r;
        }

        private static bool IsReply(byte[] resp)
        {
            if (resp == null || resp.Length < 8) return false;
            return resp[0] == 0x27 && resp[7] >= 1 && resp[7] <= 100;
        }
    }

    /// <summary>
    /// Plantronics BT600（047F:02EE）。64 字节报文，报告 ID 0x07：
    /// [8]=操作（0x02 GET）[9]=组（0x0A 电量）[10]=项（0x1A 电池）。
    /// 回包 [11]=当前档位 [12]=总档位数 [13]=充电，百分比 = 档位*100/(总档位-1)。
    /// </summary>
    public sealed class PlantronicsProtocol : BatteryProtocol
    {
        public override string Name { get { return "Plantronics"; } }
        public override int Priority { get { return 49; } }

        public override bool Matches(HidGroup group)
        {
            return group.Vid == 0x047F && group.Pid == 0x02EE;
        }

        public override BatteryReading Probe(HidGroup group)
        {
            HidInterface iface = null;
            for (int i = 0; i < group.Interfaces.Count; i++)
            {
                HidInterface x = group.Interfaces[i];
                if (x.OutputLength >= 16 && x.InputLength >= 16) { iface = x; break; }
            }
            if (iface == null) return null;

            int length = iface.OutputLength > 0 ? iface.OutputLength : 64;
            byte[] req = new byte[length];
            req[0] = 0x07;
            req[8] = 0x02;   // OP_GET
            req[9] = 0x0A;   // GRP_BATTERY
            req[10] = 0x1A;  // ITEM_BATTERY

            byte[] resp = HidQuery.RunPush(iface, req, length, 700, 3, 150, IsReply);
            if (resp == null) return null;

            int offset = resp[0] == 0x07 ? 1 : 0;
            if (resp.Length < offset + 14) return null;

            int level = resp[offset + 11];
            int count = resp[offset + 12];
            if (count < 2) return null;

            int percent = level * 100 / (count - 1);
            if (percent > 100) percent = 100;

            BatteryReading r = new BatteryReading();
            r.RawPercent = percent;
            r.Percent = Clamp(percent);
            bool charging = resp[offset + 13] != 0;
            r.Charge = charging
                ? (r.Percent >= 100 ? ChargeState.Full : ChargeState.Charging)
                : ChargeState.Discharging;
            r.Protocol = Name;
            r.Ok = true;
            return r;
        }

        private static bool IsReply(byte[] resp)
        {
            if (resp == null || resp.Length < 14) return false;
            int offset = resp[0] == 0x07 ? 1 : 0;
            if (resp.Length < offset + 14) return false;
            if (resp[offset + 8] != 0x03) return false;   // OP_GET_REPLY
            return resp[offset + 12] >= 2;
        }
    }

    /// <summary>
    /// MCHOSE X9 / V9 Pro 耳机。厂商集合 0xFF90 / 0x0001，报告 ID 0x55。
    /// 请求 55 65 01，应答 [0]=0x55 [1]=0x65 [3]=0x02，[2] 是百分比。
    /// 注意这和 MCHOSE 鼠标的 0xFF01 通道完全是两套。
    /// </summary>
    public sealed class MchoseHeadsetProtocol : BatteryProtocol
    {
        public override string Name { get { return "MCHOSE 耳机"; } }
        public override int Priority { get { return 51; } }

        public override bool Matches(HidGroup group)
        {
            if (group.Vid != 0x3837 && group.Vid != 0x291D) return false;
            return group.ByUsage(0xFF90, 0x0001) != null;
        }

        public override BatteryReading Probe(HidGroup group)
        {
            HidInterface iface = group.ByUsage(0xFF90, 0x0001);
            if (iface == null) return null;

            int length = iface.OutputLength > 0 ? iface.OutputLength : 64;
            byte[] req = new byte[length];
            req[0] = 0x55;
            req[1] = 0x65;
            req[2] = 0x01;

            byte[] resp = HidQuery.Run(iface, req, 3, 700, 150, IsReply);
            if (resp == null) return null;

            BatteryReading r = new BatteryReading();
            r.RawPercent = resp[2];
            r.Percent = Clamp(resp[2]);
            r.Charge = r.Percent >= 100 ? ChargeState.Full : ChargeState.Discharging;
            r.Protocol = Name;
            r.Ok = true;
            return r;
        }

        private static bool IsReply(byte[] resp)
        {
            if (resp == null || resp.Length < 4) return false;
            if (resp[0] != 0x55 || resp[1] != 0x65) return false;
            if (resp[3] != 0x02) return false;
            return resp[2] >= 1 && resp[2] <= 100;
        }
    }

    /// <summary>
    /// SteelSeries 老一代（Arctis 1 / 7 / 9 / Pro Wireless）。31 字节包。
    /// 每个型号的「问哪个字节、哪个字节是状态、离线值是多少」都不一样，
    /// 所以参数做成了按 PID 查表，逻辑只有一份。
    /// </summary>
    public sealed class SteelSeriesLegacyProtocol : BatteryProtocol
    {
        private sealed class Spec
        {
            public byte Cmd0, Cmd1;
            public int ResponseSize;
            public int BatteryByte;
            public int StatusByte;
            public int OfflineValue;
            public int ChargingValue;
            public int Min, Max;

            public Spec(byte c0, byte c1, int size, int batteryByte, int statusByte,
                int offline, int charging, int min, int max)
            {
                Cmd0 = c0; Cmd1 = c1; ResponseSize = size;
                BatteryByte = batteryByte; StatusByte = statusByte;
                OfflineValue = offline; ChargingValue = charging;
                Min = min; Max = max;
            }
        }

        public override string Name { get { return "SteelSeries"; } }
        public override int Priority { get { return 53; } }

        public override bool Matches(HidGroup group)
        {
            if (group.Vid != 0x1038) return false;
            if (SteelSeriesNovaProtocol.IsNovaPid(group.Pid)) return false;   // Nova 走另一套
            return SpecFor(group.Pid) != null;
        }

        public override BatteryReading Probe(HidGroup group)
        {
            Spec spec = SpecFor(group.Pid);
            if (spec == null) return null;

            HidInterface iface = null;
            for (int i = 0; i < group.Interfaces.Count; i++)
            {
                HidInterface x = group.Interfaces[i];
                if (x.OutputLength >= 2 && x.InputLength >= 2) { iface = x; break; }
            }
            if (iface == null) return null;

            int length = iface.OutputLength > 0 ? iface.OutputLength : 31;
            if (length > 31) length = 31;
            byte[] req = new byte[length];
            req[0] = spec.Cmd0;
            if (length > 1) req[1] = spec.Cmd1;

            int readLength = iface.InputLength > 0 ? iface.InputLength : spec.ResponseSize;
            byte[] resp = HidQuery.RunPush(iface, req, readLength, 700, 3, 150,
                delegate(byte[] x) { return x != null && x.Length > spec.BatteryByte; });
            if (resp == null) return null;

            int offset = 0;
            if (resp.Length > spec.BatteryByte + 1 && resp.Length > spec.ResponseSize) offset = 1;
            if (resp.Length <= offset + spec.BatteryByte) return null;

            int raw = resp[offset + spec.BatteryByte];
            if (spec.StatusByte >= 0 && resp.Length > offset + spec.StatusByte)
            {
                int status = resp[offset + spec.StatusByte];
                if (status == spec.OfflineValue) return null;
            }

            int percent = raw;
            if (spec.Max > spec.Min && spec.Max != 100)
                percent = (raw - spec.Min) * 100 / (spec.Max - spec.Min);
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;

            BatteryReading r = new BatteryReading();
            r.RawPercent = percent;
            r.Percent = Clamp(percent);
            bool charging = false;
            if (spec.StatusByte >= 0 && resp.Length > offset + spec.StatusByte)
                charging = resp[offset + spec.StatusByte] == spec.ChargingValue;
            r.Charge = charging
                ? (r.Percent >= 100 ? ChargeState.Full : ChargeState.Charging)
                : ChargeState.Discharging;
            r.Protocol = Name;
            r.Ok = true;
            return r;
        }

        private static Spec SpecFor(ushort pid)
        {
            switch (pid)
            {
                case 0x12B3: case 0x12B6: case 0x12D7: case 0x12D5:   // Arctis 1
                    return new Spec(0x06, 0x12, 8, 3, 2, 0x01, 0xFF, 0, 100);
                case 0x1260: case 0x12AD: case 0x1252: case 0x1280:   // Arctis 7
                    return new Spec(0x06, 0x18, 8, 2, -1, -1, -1, 0, 100);
                case 0x12C2: case 0x12C4:                             // Arctis 9
                    return new Spec(0x00, 0x20, 12, 3, 4, 0xFF, 0x01, 0, 100);
                case 0x1290:                                          // Arctis Pro Wireless
                    return new Spec(0x40, 0xAA, 8, 1, -1, -1, -1, 0, 100);
                default:
                    return null;
            }
        }
    }

    /// <summary>
    /// SteelSeries Nova 一代（Arctis 7+ / Nova 3 / 5 / 7 / 7P / Nova Pro Wireless）。
    /// 64 字节包，发 {0x00, 0x20} 问音频设置，回包里 [2]=电量 [3]=状态
    /// （0 离线、1/2 充电）。少数 PID 的电量是 0..4 的档位，要换算成百分比。
    /// </summary>
    public sealed class SteelSeriesNovaProtocol : BatteryProtocol
    {
        private static readonly ushort[] NovaPids = new ushort[]
        {
            0x220E, 0x2212, 0x2216, 0x2236,     // Arctis 7+
            0x12EC,                             // Nova 3
            0x2269, 0x226D,                     // Nova 3P
            0x2232, 0x2253,                     // Nova 5
            0x2202, 0x2206, 0x223A, 0x2258, 0x227A, 0x227E, 0x229E,
            0x22A1, 0x22A4, 0x22A5, 0x22A9, 0x22AD,   // Nova 7
            0x220A, 0x2298, 0x22A7,             // Nova 7P
            0x12E0, 0x12E5,                     // Nova Pro Wireless
            0x230A                              // GameBuds
        };

        // 这几个 PID 报的是 0..4 档位而不是百分比
        private static readonly ushort[] DiscretePids = new ushort[]
        {
            0x2202, 0x2206, 0x220A, 0x223A, 0x227A, 0x22A4
        };

        public static bool IsNovaPid(ushort pid)
        {
            for (int i = 0; i < NovaPids.Length; i++)
                if (NovaPids[i] == pid) return true;
            return false;
        }

        private static bool IsDiscrete(ushort pid)
        {
            for (int i = 0; i < DiscretePids.Length; i++)
                if (DiscretePids[i] == pid) return true;
            return false;
        }

        public override string Name { get { return "SteelSeries Nova"; } }
        public override int Priority { get { return 52; } }

        public override bool Matches(HidGroup group)
        {
            return group.Vid == 0x1038 && IsNovaPid(group.Pid);
        }

        public override BatteryReading Probe(HidGroup group)
        {
            HidInterface iface = group.ByOutput(64);
            if (iface == null) return null;

            int length = iface.OutputLength;
            byte[] req = new byte[length];
            req[0] = 0x00;
            req[1] = 0x20;

            int readLength = iface.InputLength > 0 ? iface.InputLength : 64;
            byte[] resp = HidQuery.RunPush(iface, req, readLength, 800, 3, 150, IsReply);
            if (resp == null) return null;

            int offset = resp[0] == 0x20 ? 0 : 1;
            if (resp.Length <= offset + 3) return null;

            int raw = resp[offset + 2];
            int status = resp[offset + 3];
            if (status == 0x00) return null;   // 离线

            int percent = raw;
            if (IsDiscrete(group.Pid)) percent = raw * 100 / 4;
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;

            BatteryReading r = new BatteryReading();
            r.RawPercent = percent;
            r.Percent = Clamp(percent);
            bool charging = status == 0x01 || status == 0x02;
            r.Charge = charging
                ? (r.Percent >= 100 ? ChargeState.Full : ChargeState.Charging)
                : ChargeState.Discharging;
            r.Protocol = Name;
            r.Ok = true;
            return r;
        }

        private static bool IsReply(byte[] resp)
        {
            if (resp == null || resp.Length < 4) return false;
            return resp[0] == 0x20 || resp[0] == 0xB0;
        }
    }

    /// <summary>所有已知协议，按 Priority 升序尝试。</summary>
    public static class ProtocolRegistry
    {
        private static List<BatteryProtocol> _all;

        public static List<BatteryProtocol> All()
        {
            if (_all != null) return _all;

            List<BatteryProtocol> list = new List<BatteryProtocol>();
            list.Add(new AulaProtocol());
            list.Add(new Compx64Protocol());
            list.Add(new Compx17Protocol());
            list.Add(new SonyInzoneBudsProtocol());
            list.Add(new AudezeProtocol());
            list.Add(new CorsairVirtuosoProtocol());
            list.Add(new CorsairVoidProtocol());
            list.Add(new JabraGnpProtocol());
            list.Add(new LenovoVoipProtocol());
            list.Add(new RazerMouseProtocol());
            list.Add(new RazerHeadsetProtocol());
            list.Add(new MchoseProtocol());
            list.Add(new NinjutsoProtocol());
            list.Add(new LamzuProtocol());
            list.Add(new AsusProtocol());
            list.Add(new HyperXProtocol());
            list.Add(new BloodyProtocol());
            list.Add(new AttackSharkProtocol());
            list.Add(new PlantronicsProtocol());
            list.Add(new LogitechProtocol());
            list.Add(new MchoseHeadsetProtocol());
            list.Add(new SteelSeriesNovaProtocol());
            list.Add(new SteelSeriesLegacyProtocol());
            list.Add(new StandardBatteryProtocol());

            list.Sort(delegate(BatteryProtocol a, BatteryProtocol b)
            {
                return a.Priority.CompareTo(b.Priority);
            });

            _all = list;
            return _all;
        }

        /// <summary>按顺序找出第一个认领这台设备的协议，然后真的去问一次。</summary>
        public static BatteryReading Probe(HidGroup group)
        {
            List<BatteryProtocol> all = All();
            for (int i = 0; i < all.Count; i++)
            {
                BatteryProtocol p = all[i];
                bool matches = false;
                try { matches = p.Matches(group); }
                catch { matches = false; }
                if (!matches) continue;

                try
                {
                    BatteryReading r = p.Probe(group);
                    if (r != null) return r;
                }
                catch { }
            }
            return null;
        }
    }
}
