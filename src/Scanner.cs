using System;
using System.Collections.Generic;

namespace Volt
{
    /// <summary>一台设备在界面上的完整状态。</summary>
    public sealed class DeviceState
    {
        /// <summary>型号记录。可能是表里的，也可能是厂商通配兜底生成的。</summary>
        public DeviceModel Model;

        /// <summary>去重用的身份。同一台设备的接收器和数据线共用同一个 Key。</summary>
        public string Key = "";

        public bool Present;
        public string Transport = "";
        public BatteryReading Reading;
        public string Error = "";

        /// <summary>这次没读到，显示的是上一次成功的数值。</summary>
        public bool Stale;
        public DateTime LastGood = DateTime.MinValue;

        /// <summary>
        /// 显示名。设备自己报了型号就用它——罗技一个接收器上可能挂着好几个鼠标，
        /// 表里的「Lightspeed 接收器」对用户没有意义。
        /// </summary>
        public string DisplayName
        {
            get
            {
                if (Reading != null && Reading.ModelName != null && Reading.ModelName.Length > 0)
                    return Reading.ModelName;
                return Model != null ? Model.DisplayName : "";
            }
        }

        public string Kind
        {
            get { return Model != null ? Model.Kind : "外设"; }
        }

        /// <summary>真无线耳塞。只用来挑图标。</summary>
        public bool Earbuds
        {
            get { return Model != null && Model.Earbuds; }
        }

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

        /// <summary>分体式耳机的三路电量，没读到的那一路是 -1。</summary>
        public int LeftPercent
        {
            get { return Reading != null && Reading.Ok ? Reading.LeftPercent : -1; }
        }

        public int RightPercent
        {
            get { return Reading != null && Reading.Ok ? Reading.RightPercent : -1; }
        }

        public int CasePercent
        {
            get { return Reading != null && Reading.Ok ? Reading.CasePercent : -1; }
        }

        public bool HasChannels
        {
            get { return Reading != null && Reading.Ok && Reading.HasChannels; }
        }
    }

    public static class Scanner
    {
        // 上一次成功读到的东西，按设备 Key 存。
        // 键盘睡下去不应答的时候，界面继续显示最后一次读到的电量，而不是跳成"没数据"。
        private static readonly Dictionary<string, BatteryReading> _cache = new Dictionary<string, BatteryReading>();
        private static readonly Dictionary<string, string> _cacheTransport = new Dictionary<string, string>();
        private static readonly Dictionary<string, DateTime> _cacheTime = new Dictionary<string, DateTime>();

        private static readonly string CacheFile = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Volt", "last-readings.tsv");

        // 每台设备一个过滤器。固件报出来的电量会在相邻几个百分点之间来回抖，
        // 直接显示就是「90 → 80 → 90」。过滤器把这种毛刺压掉，只放行真实变化。
        private static readonly Dictionary<string, BatteryFilter> _filters = new Dictionary<string, BatteryFilter>();

        // 上次成功采样的时间。用来判断两次读数之间隔了多久。
        private static readonly Dictionary<string, DateTime> _lastSample = new Dictionary<string, DateTime>();

        // 本次运行里见过哪些设备。用来识别「设备从无到有」这种重新出现的情况。
        private static readonly Dictionary<string, bool> _seen = new Dictionary<string, bool>();

        // 上一轮出现过、这一轮没出现的设备 Key。界面靠它决定要不要留一行灰的。
        private static readonly HashSet<string> _lastSeen = new HashSet<string>();

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
                    int mv; if (!int.TryParse(p[3], out mv)) r.VoltageMv = mv;
                    r.ModelName = p[6];
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
                        kv.Value.ModelName ?? ""
                    }));
                }
                System.IO.File.WriteAllText(CacheFile, sb.ToString());
            }
            catch { }
        }

        /// <summary>
        /// 这台设备是否暴露标准 HID 电量集合（Usage Page 0x0006 / Usage 0x0020）。
        /// 有的话不用任何私有协议就能读，值得当成一台设备列出来。
        /// </summary>
        private static bool HasStandardBattery(HidGroup g)
        {
            for (int i = 0; i < g.Interfaces.Count; i++)
            {
                HidInterface x = g.Interfaces[i];
                if (x.UsagePage == 0x0006 && x.Usage == 0x0020) return true;
            }
            return false;
        }

        /// <summary>
        /// 把散装的 HID 集合按物理设备归组。
        /// 一个接收器会同时暴露键盘、鼠标、厂商自定义好几个集合，
        /// 它们 VID:PID 相同，是同一台设备。
        /// </summary>
        private static List<HidGroup> Group(List<HidInterface> all)
        {
            List<HidGroup> groups = new List<HidGroup>();
            Dictionary<string, HidGroup> index = new Dictionary<string, HidGroup>();

            for (int i = 0; i < all.Count; i++)
            {
                HidInterface x = all[i];
                string id = x.Vid.ToString("X4") + ":" + x.Pid.ToString("X4");
                HidGroup g;
                if (!index.TryGetValue(id, out g))
                {
                    g = new HidGroup();
                    g.Vid = x.Vid;
                    g.Pid = x.Pid;
                    g.Product = x.Product ?? "";
                    g.Manufacturer = x.Manufacturer ?? "";
                    index[id] = g;
                    groups.Add(g);
                }
                g.Interfaces.Add(x);
            }
            return groups;
        }

        /// <summary>
        /// 扫描全部设备。每台物理设备只返回一条结果——
        /// 即使接收器和数据线同时插着，也只挑当前真正在用的那个连接。
        /// </summary>
        public static List<DeviceState> Scan()
        {
            LoadCache();
            List<HidInterface> all = HidNative.Enumerate();
            List<HidGroup> groups = Group(all);

            // 按设备身份合并：同一台鼠标的接收器组和有线组归到同一条。
            Dictionary<string, DeviceState> byKey = new Dictionary<string, DeviceState>();
            List<string> order = new List<string>();

            // 探测顺序：先看表里认识的，再试协议。
            // 不认识的厂商直接跳过，免得把摄像头、读卡器也列进来。
            List<BatteryProtocol> protocols = ProtocolRegistry.All();

            for (int gi = 0; gi < groups.Count; gi++)
            {
                HidGroup g = groups[gi];

                DeviceModel model = DeviceTable.Lookup(g.Vid, g.Pid);
                if (model == null)
                {
                    // 表里没有的型号：只有自带标准 HID 电量（Usage Page 0x0006 / Usage 0x0020）
                    // 的设备才值得列出来——那种设备不需要任何私有协议，插上就该显示，
                    // 不该因为厂商名不在表里就被丢掉。其余不认识的厂商跳过，
                    // 免得把摄像头、读卡器、U 盘都列进来。
                    bool standardBattery = HasStandardBattery(g);
                    model = DeviceTable.Fallback(g.Vid, g.Pid, g.Product, standardBattery);
                }
                if (model == null) continue;

                bool wired = model.IsWired(g.Pid);
                string transport = wired ? "USB 有线" : TransportOf(g, false);

                BatteryReading reading = null;
                for (int pi = 0; pi < protocols.Count && reading == null; pi++)
                {
                    BatteryProtocol proto = protocols[pi];
                    bool matches = false;
                    try { matches = proto.Matches(g); }
                    catch { matches = false; }
                    if (!matches) continue;

                    try { reading = proto.Probe(g); }
                    catch { reading = null; }

                    if (reading != null && !reading.Ok && !reading.Asleep) reading = null;
                    if (reading != null && reading.Protocol.Length == 0) reading.Protocol = proto.Name;
                }

                if (reading != null && reading.Asleep)
                {
                    // 接收器在，配对的设备睡着了。不覆盖缓存，只标记。
                    reading.Ok = false;
                }

                DeviceState st;
                if (!byKey.TryGetValue(model.Key, out st))
                {
                    st = new DeviceState();
                    st.Model = model;
                    st.Key = model.Key;
                    byKey[model.Key] = st;
                    order.Add(model.Key);
                }

                st.Present = true;

                if (reading != null && reading.Ok)
                {
                    // 两个连接都能读到时，优先用有线在充电的那条。
                    bool better = st.Reading == null || !st.Reading.Ok
                        || (reading.Charge == ChargeState.Charging
                            && st.Reading.Charge != ChargeState.Charging
                            && wired);
                    if (better)
                    {
                        st.Reading = reading;
                        st.Transport = transport;
                        st.Error = "";
                    }
                }
                else if (st.Reading == null || !st.Reading.Ok)
                {
                    if (reading != null && reading.Note.Length > 0) st.Error = reading.Note;
                    else if (model.Note.Length > 0) st.Error = model.Note;
                    else if (st.Error.Length == 0) st.Error = "设备未应答";
                    if (st.Transport.Length == 0) st.Transport = transport;
                }
            }

            List<DeviceState> result = new List<DeviceState>();

            // 有些设备被型号表标注成「拿不到电量」（model.Note 非空），比如那些
            // 2.4G 音频接收器在硬件层面就不上报电量的耳机。这种设备留在界面上
            // 只是一行永远读数的占位，对用户没用，所以从来读到过值的直接不显示。
            // 一旦某天真读到了值（协议后来补上、或换了连接方式），它照样会显示。
            HashSet<string> hidden = new HashSet<string>();

            for (int i = 0; i < order.Count; i++)
            {
                string key = order[i];
                DeviceState st = byKey[key];

                if (st.Model != null && st.Model.Note.Length > 0
                    && (st.Reading == null || !st.Reading.Ok))
                {
                    hidden.Add(key);
                    continue;
                }

                if (st.Reading != null && st.Reading.Ok)
                {
                    ApplyFilter(st);
                    st.LastGood = DateTime.Now;
                    st.Stale = false;

                    _cache[key] = st.Reading;
                    _cacheTransport[key] = st.Transport;
                    _cacheTime[key] = st.LastGood;
                }
                else if (_cache.ContainsKey(key))
                {
                    // 这次没应答：沿用上次的读数，标成「上次 HH:mm」。
                    st.Reading = _cache[key];
                    st.Transport = _cacheTransport.ContainsKey(key) ? _cacheTransport[key] : st.Transport;
                    st.LastGood = _cacheTime.ContainsKey(key) ? _cacheTime[key] : DateTime.MinValue;
                    st.Stale = true;
                    if (st.Error.Length == 0) st.Error = "设备未应答";
                }

                result.Add(st);
            }

            // 上一轮在、这一轮不在的设备：留一行「未连接」，让用户知道它掉线了，
            // 而不是凭空消失。
            foreach (string key in _lastSeen)
            {
                if (byKey.ContainsKey(key)) continue;
                if (hidden.Contains(key)) continue;
                DeviceState st = new DeviceState();
                st.Key = key;
                st.Model = new DeviceModel();
                st.Model.Key = key;
                st.Model.Name = CacheName(key);
                st.Model.Kind = "外设";
                st.Present = false;
                st.Error = "未连接";
                if (_cache.ContainsKey(key))
                {
                    st.Reading = _cache[key];
                    st.Transport = _cacheTransport.ContainsKey(key) ? _cacheTransport[key] : "";
                    st.LastGood = _cacheTime.ContainsKey(key) ? _cacheTime[key] : DateTime.MinValue;
                    st.Stale = true;
                    if (st.Model.Name.Length == 0) st.Model.Name = st.Reading.ModelName;
                }
                result.Add(st);
            }

            _lastSeen.Clear();
            foreach (string key in order)
            {
                if (hidden.Contains(key)) continue;
                _lastSeen.Add(key);
            }

            SaveCache();
            return result;
        }

        private static string CacheName(string key)
        {
            BatteryReading r;
            if (_cache.TryGetValue(key, out r) && r != null && r.ModelName != null) return r.ModelName;
            return "";
        }

        private static string TransportOf(HidGroup g, bool fallbackWired)
        {
            if (fallbackWired) return "USB 有线";
            for (int i = 0; i < g.Interfaces.Count; i++)
            {
                string path = g.Interfaces[i].Path ?? "";
                if (path.IndexOf("bth", StringComparison.OrdinalIgnoreCase) >= 0) return "蓝牙";
            }
            return "2.4G 接收器";
        }

        private static void ApplyFilter(DeviceState st)
        {
            // 过滤：显示值只走过滤器，原始值留在 RawPercent 里备查。
            // 间隔也一起喂进去——窗口收起时轮询会变慢，间隔太久的两次读数
            // 之间完全可能真的充过电，这时候不该按「毛刺」压掉。
            BatteryFilter filter = GetFilter(st.Key);

            // 换了连接方式（接收器 ↔ 有线），或者这台设备刚从「未连接」回来，
            // 等同于电池被取下来重装：中间发生了什么我们一无所知，
            // 直接以当前读数为新起点，否则旧值会一直压着显示不动。
            bool rebased = false;
            string prevTransport;
            if (_cacheTransport.TryGetValue(st.Key, out prevTransport)
                && prevTransport != st.Transport)
            {
                rebased = true;
            }
            if (!_seen.ContainsKey(st.Key)) rebased = true;
            _seen[st.Key] = true;

            long elapsed = 0;
            DateTime lastTick;
            if (_lastSample.TryGetValue(st.Key, out lastTick))
            {
                elapsed = (long)(DateTime.Now - lastTick).TotalMilliseconds;
            }
            _lastSample[st.Key] = DateTime.Now;

            if (rebased) filter.Resync(st.Reading.RawPercent);
            st.Reading.Percent = filter.Apply(st.Reading.RawPercent, st.Reading.Charge, elapsed);
        }

        /// <summary>
        /// 取某台设备的过滤器。第一次用时用缓存里的上次读数做初值——
        /// 否则程序一启动，第一次读到的毛刺会被当成「初始值」直接显示。
        /// </summary>
        private static BatteryFilter GetFilter(string key)
        {
            BatteryFilter f;
            if (_filters.TryGetValue(key, out f)) return f;

            int seed = -1;
            BatteryReading cached;
            if (_cache.TryGetValue(key, out cached) && cached != null) seed = cached.Percent;
            f = new BatteryFilter(seed);
            _filters[key] = f;
            return f;
        }
    }
}
