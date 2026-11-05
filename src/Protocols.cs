using System;
using System.Collections.Generic;
using System.Linq;

namespace Volt
{
    public enum ChargeState
    {
        Unknown = 0,
        Discharging = 1,
        Charging = 2,
        Full = 3
    }

    /// <summary>一次读取的结果。</summary>
    public sealed class BatteryReading
    {
        public int Percent = -1;
        public ChargeState Charge = ChargeState.Unknown;
        public int VoltageMv = -1;
        public string Note = "";
        public bool Ok = false;

        public bool HasPercent
        {
            get { return Percent >= 0; }
        }
    }

    /// <summary>
    /// 一个可查询的 HID 接口特征。设备用哪个连接方式就用哪一组特征去匹配。
    /// </summary>
    public sealed class Endpoint
    {
        public ushort Vid;
        public ushort Pid;
        public ushort UsagePage;
        public ushort Usage;
        public int InputLength;
        public int OutputLength;

        /// <summary>连接方式显示文字，例如「2.4G 接收器」「USB 有线」。</summary>
        public string Transport = "";

        public bool Matches(HidInterface i)
        {
            return i.Vid == Vid && i.Pid == Pid
                && i.UsagePage == UsagePage && i.Usage == Usage
                && i.InputLength == InputLength && i.OutputLength == OutputLength;
        }
    }

    /// <summary>
    /// 一台物理设备。可以有多个 Endpoint（接收器 / 数据线），
    /// 但对外永远只呈现为一条记录。
    /// </summary>
    public sealed class DeviceDefinition
    {
        public string Key = "";
        public string DisplayName = "";
        public string Kind = "";
        public string Vendor = "";
        public Endpoint[] Endpoints = new Endpoint[0];

        public BatteryReading Read(HidInterface iface)
        {
            if (Key == "atk-compx") return AtkCompx.Read(iface);
            if (Key == "aula-24g") return Aula24G.Read(iface);
            return new BatteryReading();
        }
    }

    /// <summary>
    /// 发帧取应答的公共逻辑。每次尝试都用全新句柄：
    /// 一次超时留下的未完成读取会占住句柄并抢走下一次应答，
    /// 关掉句柄就能把它彻底取消。
    /// </summary>
    internal static class HidQuery
    {
        public static bool LastOpenOk = true;

        public static byte[] Run(HidInterface iface, byte[] request, int attempts, int timeoutMs,
            int gapMs, Func<byte[], bool> accept)
        {
            LastOpenOk = true;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                HidNative.Handle h = HidNative.Handle.Open(iface.Path);
                if (h == null || !h.IsOpen)
                {
                    LastOpenOk = false;
                    if (h != null) h.Dispose();
                    return null;
                }

                byte[] got = null;
                try
                {
                    got = h.Transact(request, iface.InputLength, timeoutMs);
                }
                finally
                {
                    h.Close();
                }

                if (got != null && accept(got)) return got;
                if (attempt < attempts - 1) System.Threading.Thread.Sleep(gapMs);
            }
            return null;
        }
    }
    /// <summary>
    /// ATK / VXE / 狼蛛 用的 Compx 方案帧（17 字节，Report ID 0x08）。
    /// 布局：[0]=0x08 [1]=命令 [2]=状态 [3..4]=地址 [5]=长度 [6..15]=数据 [16]=校验
    /// 校验 = 0x55 - sum(前 16 字节)，等价于整帧 16 字节和为 0x55。
    /// 电量在 [6]，充电位在 [7]（1=充电中），电压大端在 [8..9]（mV）。
    /// </summary>
    public static class AtkCompx
    {
        private static bool IsAtkReply(byte[] got)
        {
            return got != null && got.Length >= 10 && got[0] == 0x08 && got[1] == 0x04;
        }
        public static BatteryReading Read(HidInterface iface)
        {
            BatteryReading r = new BatteryReading();
            int len = iface.OutputLength;
            if (len < 17) return r;

            byte[] req = new byte[len];
            req[0] = 0x08;
            req[1] = 0x04;

            // 校验放在这一帧实际使用的那一位上
            int sum = 0;
            for (int i = 0; i < 16; i++) sum += req[i];
            req[16] = (byte)((0x55 - (sum & 0xFF)) & 0xFF);
            if (len > 17)
            {
                // 20 字节接口不收这个帧，但仍然把校验算完整
                req[len - 1] = req[16];
                req[16] = 0x00;
                sum = 0;
                for (int i = 0; i < len - 1; i++) sum += req[i];
                req[len - 1] = (byte)((0x55 - (sum & 0xFF)) & 0xFF);
            }

            byte[] resp = HidQuery.Run(iface, req, 4, 400, 120, new Func<byte[], bool>(IsAtkReply));
            if (resp == null && !HidQuery.LastOpenOk)
            {
                r.Note = "打不开接口";
                return r;
            }

            if (resp == null)
            {
                r.Note = "设备未应答（可能正在休眠，动一下就好）";
                return r;
            }

            r.Percent = resp[6];
            int chargeByte = resp[7];
            if (chargeByte == 1) r.Charge = ChargeState.Charging;
            else r.Charge = ChargeState.Discharging;

            if (resp.Length >= 10)
            {
                int mv = (resp[8] << 8) | resp[9];
                if (mv > 2000 && mv < 5000) r.VoltageMv = mv;
            }

            if (r.Percent == 100 && r.Charge == ChargeState.Charging) r.Charge = ChargeState.Full;
            r.Ok = r.Percent >= 0 && r.Percent <= 100;
            if (!r.Ok) r.Percent = -1;
            return r;
        }
    }

    /// <summary>
    /// 狼蛛 / AULA 20 字节帧（Report ID 0x13）。
    /// 请求：13 CA 后补零，末字节 = sum8(前 19 字节)。
    /// 响应：13 4A 01 00 02 &lt;电量&gt; &lt;状态&gt; ...
    /// 状态 0x10 = 充电中，0x01 = 未充电。
    /// </summary>
    public static class Aula24G
    {
        private static readonly byte[] Prefix = new byte[] { 0x13, 0x4A, 0x01, 0x00, 0x02 };

        public static BatteryReading Read(HidInterface iface)
        {
            BatteryReading r = new BatteryReading();
            int len = iface.OutputLength;
            if (len < 20) return r;

            byte[] req = new byte[len];
            req[0] = 0x13;
            req[1] = 0xCA;
            int sum = 0;
            for (int i = 0; i < len - 1; i++) sum += req[i];
            req[len - 1] = (byte)(sum & 0xFF);

            byte[] resp = HidQuery.Run(iface, req, 4, 600, 800, new Func<byte[], bool>(Matches));
            if (resp == null && !HidQuery.LastOpenOk)
            {
                r.Note = "打不开接口";
                return r;
            }

            if (resp == null)
            {
                r.Note = "键盘未应答（可能正在休眠，按一下键就好）";
                return r;
            }

            r.Percent = resp[5];
            int status = resp[6];
            if (status == 0x10) r.Charge = ChargeState.Charging;
            else if (status == 0x01) r.Charge = ChargeState.Discharging;
            else r.Charge = ChargeState.Unknown;

            if (r.Percent == 100 && r.Charge == ChargeState.Charging) r.Charge = ChargeState.Full;
            r.Ok = r.Percent >= 0 && r.Percent <= 100;
            if (!r.Ok) r.Percent = -1;
            return r;
        }

        private static bool Matches(byte[] resp)
        {
            if (resp == null || resp.Length < Prefix.Length + 2) return false;
            for (int i = 0; i < Prefix.Length; i++)
                if (resp[i] != Prefix[i]) return false;
            return true;
        }
    }

    /// <summary>
    /// 设备清单。这里就是「型号不能认错」的唯一依据——
    /// 全部来自本机实测，不是猜的。
    /// </summary>
    public static class Catalog
    {
        public static List<DeviceDefinition> Build()
        {
            List<DeviceDefinition> list = new List<DeviceDefinition>();

            // ---------------- ATK X1 SE ----------------
            // 2.4G 接收器 373B:10C9 与 有线/星闪 373B:1107 用的是同一套 17 字节帧，
            // 所以两者归成同一台设备，只按当前接入方式标注。
            DeviceDefinition mouse = new DeviceDefinition();
            mouse.Key = "atk-compx";
            mouse.DisplayName = "ATK X1 SE";
            mouse.Kind = "鼠标";
            mouse.Vendor = "ATK";
            mouse.Endpoints = new Endpoint[]
            {
                new Endpoint { Vid = 0x373B, Pid = 0x10C9, UsagePage = 0xFF02, Usage = 0x0002,
                               InputLength = 17, OutputLength = 17, Transport = "2.4G 接收器" },
                new Endpoint { Vid = 0x373B, Pid = 0x1107, UsagePage = 0xFF02, Usage = 0x0002,
                               InputLength = 17, OutputLength = 17, Transport = "USB 有线" }
            };
            list.Add(mouse);

            // ---------------- AULA F99 ----------------
            DeviceDefinition keyboard = new DeviceDefinition();
            keyboard.Key = "aula-24g";
            keyboard.DisplayName = "AULA F99";
            keyboard.Kind = "键盘";
            keyboard.Vendor = "AULA";
            keyboard.Endpoints = new Endpoint[]
            {
                new Endpoint { Vid = 0x3554, Pid = 0xFA09, UsagePage = 0xFF02, Usage = 0x0002,
                               InputLength = 20, OutputLength = 20, Transport = "2.4G 接收器" }
            };
            list.Add(keyboard);

            return list;
        }
    }
}
