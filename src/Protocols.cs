using System;
using System.Collections.Generic;

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
        /// <summary>固件原样报出来的电量，未经过滤，可能超出 0..100。</summary>
        public int RawPercent = -1;

        /// <summary>要显示的电量。由 Scanner 过滤后写入。</summary>
        public int Percent = -1;
        public ChargeState Charge = ChargeState.Unknown;
        public int VoltageMv = -1;
        public string Note = "";
        public bool Ok = false;

        /// <summary>设备自己报出来的型号。罗技接收器上可能挂着好几个鼠标，只能靠这个区分。</summary>
        public string ModelName = "";

        /// <summary>用的是哪套协议，显示在副标题里，排查问题时有用。</summary>
        public string Protocol = "";

        /// <summary>接收器在，但配对的设备没唤醒。界面显示上次读数，不当成「没设备」。</summary>
        public bool Asleep = false;

        // ---- 分体式耳机的三路电量 ----
        //
        // 真无线耳塞（TWS）其实是三块电池：左耳、右耳、充电盒。
        // 绝大多数开源实现把它们压成一个数（取左右最小值），因为上层数据结构只有一个字段。
        // Volt 这边保留三路，界面按「左 / 右 / 盒」分三行小字显示。
        // -1 表示这一路没读到（例如盒子没插耳机、或者协议本身不报）。

        public int LeftPercent = -1;
        public int RightPercent = -1;
        public int CasePercent = -1;

        public bool HasChannels
        {
            get { return LeftPercent >= 0 || RightPercent >= 0 || CasePercent >= 0; }
        }

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
    /// 同一个 USB 设备暴露出来的所有 HID 集合。
    /// 一个接收器通常有键盘、鼠标、厂商自定义等好几个集合，协议只在其中一个上说话。
    /// </summary>
    public sealed class HidGroup
    {
        public ushort Vid;
        public ushort Pid;
        public string Product = "";
        public string Manufacturer = "";
        public List<HidInterface> Interfaces = new List<HidInterface>();

        public string VidPid
        {
            get { return Vid.ToString("X4") + ":" + Pid.ToString("X4"); }
        }

        /// <summary>按 Usage Page / Usage 找集合。</summary>
        public HidInterface ByUsage(ushort page, ushort usage)
        {
            for (int i = 0; i < Interfaces.Count; i++)
            {
                HidInterface x = Interfaces[i];
                if (x.UsagePage == page && x.Usage == usage) return x;
            }
            return null;
        }

        /// <summary>
        /// 按 Usage Page / Usage + Output 长度精确找集合。
        ///
        /// 必须按长度挑，不能只按 usage 挑：同一个接收器上常有两组同 usage 的厂商集合，
        /// 比如 ATK 的接收器同时暴露 20 字节和 17 字节两组 FF02/0002。
        /// 只按 usage 取第一个，拿到的是 20 字节那组，往里写 17 字节帧会被拒绝，
        /// 设备就永远"没应答"。
        /// </summary>
        public HidInterface ByUsage(ushort page, ushort usage, int outputLength)
        {
            for (int i = 0; i < Interfaces.Count; i++)
            {
                HidInterface x = Interfaces[i];
                if (x.UsagePage == page && x.Usage == usage && x.OutputLength == outputLength) return x;
            }
            return null;
        }

        /// <summary>找厂商自定义页（0xFF00 以上）里 Output 长度最接近 want 的集合。</summary>
        public HidInterface ByOutput(int want)
        {
            HidInterface best = null;
            int bestGap = int.MaxValue;
            for (int i = 0; i < Interfaces.Count; i++)
            {
                HidInterface x = Interfaces[i];
                if (x.UsagePage < 0xFF00) continue;
                if (x.OutputLength < want) continue;
                int gap = x.OutputLength - want;
                if (gap < bestGap) { bestGap = gap; best = x; }
            }
            return best;
        }

        /// <summary>找 Feature 报告长度够用的集合，优先厂商自定义页。</summary>
        public HidInterface ByFeature(int minLength)
        {
            HidInterface fallback = null;
            for (int i = 0; i < Interfaces.Count; i++)
            {
                HidInterface x = Interfaces[i];
                if (x.FeatureLength < minLength) continue;
                if (x.UsagePage >= 0xFF00) return x;
                if (fallback == null) fallback = x;
            }
            return fallback;
        }
    }

    /// <summary>
    /// 一套取电量的协议。每套协议自己决定在哪个集合上、发什么帧、怎么解析。
    /// </summary>
    public abstract class BatteryProtocol
    {
        /// <summary>显示用的协议名。</summary>
        public abstract string Name { get; }

        /// <summary>数字越小越先尝试。厂商专属协议排在通用协议前面。</summary>
        public abstract int Priority { get; }

        /// <summary>这个设备像不像用这套协议的。只看 VID / 集合形状，不发帧。</summary>
        public abstract bool Matches(HidGroup group);

        /// <summary>真的去问一次。读不到返回 null。</summary>
        public abstract BatteryReading Probe(HidGroup group);

        protected static int Clamp(int value)
        {
            if (value < 0) return 0;
            if (value > 100) return 100;
            return value;
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

        /// <summary>
        /// Feature 报告一问一答：SetFeature 发命令，等一下，GetFeature 取回同一份缓冲区。
        /// 长度必须等于接口的 FeatureReportByteLength。
        /// </summary>
        public static byte[] RunFeature(HidInterface iface, byte[] request, int delayMs,
            int attempts, int gapMs, Func<byte[], bool> accept)
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
                    if (h.SetFeature(request))
                    {
                        System.Threading.Thread.Sleep(delayMs);
                        byte[] response = new byte[request.Length];
                        response[0] = request[0];
                        if (h.GetFeature(response)) got = response;
                    }
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

        /// <summary>
        /// 只写不读，然后等设备主动推一个 Input 报告（推送式协议用）。
        /// </summary>
        public static byte[] RunPush(HidInterface iface, byte[] request, int readLength,
            int timeoutMs, int attempts, int gapMs, Func<byte[], bool> accept)
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
                    if (request != null) h.Write(request);
                    got = h.Read(readLength, timeoutMs);
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

        public static bool StartsWith(byte[] data, params byte[] prefix)
        {
            if (data == null || data.Length < prefix.Length) return false;
            for (int i = 0; i < prefix.Length; i++)
                if (data[i] != prefix[i]) return false;
            return true;
        }
    }
}
