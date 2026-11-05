using System;
using System.Collections.Generic;

namespace Volt
{
    /// <summary>一台设备在界面上的完整状态。</summary>
    public sealed class DeviceState
    {
        public DeviceDefinition Def;
        public bool Present;
        public string Transport = "";
        public BatteryReading Reading;
        public string Error = "";

        /// <summary>这次没读到，显示的是上一次成功的数值。</summary>
        public bool Stale;
        public DateTime LastGood = DateTime.MinValue;

        public int Percent
        {
            get { return Reading != null && Reading.Ok ? Reading.Percent : -1; }
        }

        public ChargeState Charge
        {
            get { return Reading != null && Reading.Ok ? Reading.Charge : ChargeState.Unknown; }
        }

        public int VoltageMv
        {
            get { return Reading != null && Reading.Ok ? Reading.VoltageMv : -1; }
        }
    }

    public static class Scanner
    {
        // 上一次成功读到的东西，按设备 Key 存。
        // 键盘睡下去不应答的时候，界面继续显示最后一次读到的电量，而不是跳成“没数据”。
        private static readonly Dictionary<string, BatteryReading> _cache = new Dictionary<string, BatteryReading>();
        private static readonly Dictionary<string, string> _cacheTransport = new Dictionary<string, string>();
        private static readonly Dictionary<string, DateTime> _cacheTime = new Dictionary<string, DateTime>();

        private static readonly string CacheFile = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Volt", "last-readings.tsv");

        private static bool _loaded = false;

        /// <summary>
        /// 上次读数落盘。键盘休眠时读不到，重启程序也能显示上一次成功读到的值，
        /// 而不是一片「无数据」。
        /// </summary>
        private static void LoadCache()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                if (!System.IO.File.Exists(CacheFile)) return;
                foreach (string line in System.IO.File.ReadAllLines(CacheFile))
                {
                    string[] p = line.Split('\t');
                    if (p.Length < 7) continue;
                    BatteryReading r = new BatteryReading();
                    int pct; if (!int.TryParse(p[1], out pct)) continue;
                    r.Percent = pct;
                    r.Ok = true;
                    int ch; if (int.TryParse(p[2], out ch)) r.Charge = (ChargeState)ch;
                    int mv; if (int.TryParse(p[3], out mv)) r.VoltageMv = mv;
                    _cache[p[0]] = r;
                    _cacheTransport[p[0]] = p[4];
                    DateTime dt;
                    if (DateTime.TryParse(p[5], out dt)) _cacheTime[p[0]] = dt;
                }
            }
            catch { }
        }

        private static void SaveCache()
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(CacheFile);
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);

                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                foreach (var kv in _cache)
                {
                    string tr = _cacheTransport.ContainsKey(kv.Key) ? _cacheTransport[kv.Key] : "";
                    DateTime dt = _cacheTime.ContainsKey(kv.Key) ? _cacheTime[kv.Key] : DateTime.MinValue;
                    sb.AppendLine(string.Join("\t", new string[] {
                        kv.Key,
                        kv.Value.Percent.ToString(),
                        ((int)kv.Value.Charge).ToString(),
                        kv.Value.VoltageMv.ToString(),
                        tr,
                        dt.ToString("o"),
                        kv.Value.Note ?? ""
                    }));
                }
                System.IO.File.WriteAllText(CacheFile, sb.ToString());
            }
            catch { }
        }

        /// <summary>
        /// 扫描全部设备。每台物理设备只返回一条结果——
        /// 即使接收器和数据线同时插着，也只挑当前真正在用的那个连接。
        /// </summary>
        public static List<DeviceState> Scan(List<DeviceDefinition> catalog)
        {
            LoadCache();
            List<HidInterface> all = HidNative.Enumerate();
            List<DeviceState> result = new List<DeviceState>();

            foreach (DeviceDefinition def in catalog)
            {
                DeviceState st = new DeviceState();
                st.Def = def;

                List<Endpoint> okEndpoints = new List<Endpoint>();
                List<BatteryReading> okReadings = new List<BatteryReading>();
                List<string> transports = new List<string>();
                string firstError = "";
                bool anyInterface = false;

                foreach (Endpoint ep in def.Endpoints)
                {
                    HidInterface found = Find(all, ep);
                    if (found == null) continue;
                    anyInterface = true;

                    BatteryReading r = def.Read(found);
                    if (r != null && r.Ok)
                    {
                        okEndpoints.Add(ep);
                        okReadings.Add(r);
                        transports.Add(ep.Transport);
                    }
                    else if (firstError.Length == 0)
                    {
                        firstError = r != null && r.Note.Length > 0 ? r.Note : "设备未应答";
                    }
                }

                if (okEndpoints.Count > 0)
                {
                    // 两个连接都能读到时，优先用有线在充电的那条
                    int pick = 0;
                    for (int i = 0; i < okEndpoints.Count; i++)
                    {
                        if (okReadings[i].Charge == ChargeState.Charging
                            && transports[i].IndexOf("有线") >= 0)
                        {
                            pick = i;
                            break;
                        }
                    }

                    st.Present = true;
                    st.Transport = transports[pick];
                    st.Reading = okReadings[pick];
                    st.LastGood = DateTime.Now;
                    st.Stale = false;

                    _cache[def.Key] = okReadings[pick];
                    _cacheTransport[def.Key] = transports[pick];
                    _cacheTime[def.Key] = st.LastGood;
                    SaveCache();
                }
                else if (anyInterface)
                {
                    // 接口在，但这次没应答：沿用上次的读数
                    st.Present = true;
                    if (_cache.ContainsKey(def.Key))
                    {
                        st.Reading = _cache[def.Key];
                        st.Transport = _cacheTransport[def.Key];
                        st.LastGood = _cacheTime[def.Key];
                        st.Stale = true;
                        st.Error = firstError;
                    }
                    else
                    {
                        st.Error = firstError.Length > 0 ? firstError : "设备未应答";
                    }
                }
                else
                {
                    st.Present = false;
                    st.Error = "未连接";
                    _cache.Remove(def.Key);
                    _cacheTransport.Remove(def.Key);
                    _cacheTime.Remove(def.Key);
                }

                result.Add(st);
            }

            return result;
        }

        private static HidInterface Find(List<HidInterface> all, Endpoint ep)
        {
            for (int i = 0; i < all.Count; i++)
            {
                if (ep.Matches(all[i])) return all[i];
            }
            return null;
        }
    }
}
