using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Volt
{
    /// <summary>
    /// 一个 HID 集合（一个设备接口）。
    /// </summary>
    public sealed class HidInterface
    {
        public string Path;
        public ushort Vid;
        public ushort Pid;
        public ushort UsagePage;
        public ushort Usage;
        public int InputLength;
        public int OutputLength;
        public int FeatureLength;
        public string Product;
        public string Manufacturer;
        public string Serial;

        public string VidPid
        {
            get { return Vid.ToString("X4") + ":" + Pid.ToString("X4"); }
        }

        public string UsageText
        {
            get { return UsagePage.ToString("X4") + "/" + Usage.ToString("X4"); }
        }
    }

    /// <summary>
    /// HID 枚举与句柄操作。所有读写都走 overlapped I/O，任何操作都不会永久阻塞。
    /// </summary>
    public static class HidNative
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;
            public Guid InterfaceClassGuid;
            public int Flags;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HIDD_ATTRIBUTES
        {
            public int Size;
            public ushort VendorID;
            public ushort ProductID;
            public ushort VersionNumber;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HIDP_CAPS
        {
            public ushort Usage;
            public ushort UsagePage;
            public ushort InputReportByteLength;
            public ushort OutputReportByteLength;
            public ushort FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
            public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes;
            public ushort NumberInputButtonCaps;
            public ushort NumberInputValueCaps;
            public ushort NumberInputDataIndices;
            public ushort NumberOutputButtonCaps;
            public ushort NumberOutputValueCaps;
            public ushort NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps;
            public ushort NumberFeatureValueCaps;
            public ushort NumberFeatureDataIndices;
        }

        [DllImport("hid.dll")]
        private static extern void HidD_GetHidGuid(out Guid guid);

        [DllImport("hid.dll")]
        private static extern bool HidD_GetAttributes(IntPtr h, ref HIDD_ATTRIBUTES a);

        [DllImport("hid.dll")]
        private static extern bool HidD_GetPreparsedData(IntPtr h, out IntPtr pp);

        [DllImport("hid.dll")]
        private static extern bool HidP_GetCaps(IntPtr pp, ref HIDP_CAPS c);

        [DllImport("hid.dll")]
        private static extern bool HidD_FreePreparsedData(IntPtr pp);

        [DllImport("hid.dll", CharSet = CharSet.Unicode)]
        private static extern bool HidD_GetProductString(IntPtr h, StringBuilder b, uint len);

        [DllImport("hid.dll", CharSet = CharSet.Unicode)]
        private static extern bool HidD_GetManufacturerString(IntPtr h, StringBuilder b, uint len);

        [DllImport("hid.dll", CharSet = CharSet.Unicode)]
        private static extern bool HidD_GetSerialNumberString(IntPtr h, StringBuilder b, uint len);

        [DllImport("hid.dll")]
        private static extern bool HidD_SetFeature(IntPtr h, byte[] buffer, uint len);

        [DllImport("hid.dll")]
        private static extern bool HidD_GetFeature(IntPtr h, byte[] buffer, uint len);

        [DllImport("hid.dll")]
        private static extern bool HidD_SetOutputReport(IntPtr h, byte[] buffer, uint len);

        [DllImport("hid.dll")]
        private static extern bool HidD_GetInputReport(IntPtr h, byte[] buffer, uint len);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(ref Guid g, IntPtr enumerator, IntPtr hwnd, uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr devInfo, ref Guid g,
            uint index, ref SP_DEVICE_INTERFACE_DATA data);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref SP_DEVICE_INTERFACE_DATA data,
            IntPtr detail, uint size, out uint required, IntPtr devInfo);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFileW(string name, uint access, uint share, IntPtr sa,
            uint disp, uint flags, IntPtr tmpl);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteFile(IntPtr h, byte[] buf, uint n, out uint written, ref System.Threading.NativeOverlapped o);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadFile(IntPtr h, byte[] buf, uint n, out uint read, ref System.Threading.NativeOverlapped o);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetOverlappedResult(IntPtr h, ref System.Threading.NativeOverlapped o, out uint transferred, bool wait);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr h, uint ms);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateEvent(IntPtr sa, bool manualReset, bool initialState, IntPtr name);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ResetEvent(IntPtr h);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CancelIo(IntPtr h);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr h);

        private const uint DIGCF_PRESENT = 0x02;
        private const uint DIGCF_DEVICEINTERFACE = 0x10;
        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 0x01;
        private const uint FILE_SHARE_WRITE = 0x02;
        private const uint OPEN_EXISTING = 3;
        private const uint FILE_FLAG_OVERLAPPED = 0x40000000;
        private const int ERROR_IO_PENDING = 997;

        private static string SafeString(IntPtr h, int which)
        {
            StringBuilder sb = new StringBuilder(512);
            bool ok;
            if (which == 0) ok = HidD_GetProductString(h, sb, 512);
            else if (which == 1) ok = HidD_GetManufacturerString(h, sb, 512);
            else ok = HidD_GetSerialNumberString(h, sb, 512);
            if (!ok) return "";
            return sb.ToString().Trim();
        }

        /// <summary>枚举系统里所有 HID 集合。</summary>
        public static List<HidInterface> Enumerate()
        {
            List<HidInterface> list = new List<HidInterface>();
            Guid hidGuid;
            HidD_GetHidGuid(out hidGuid);

            IntPtr set = SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero,
                DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
            if (set == new IntPtr(-1)) return list;

            try
            {
                SP_DEVICE_INTERFACE_DATA did = new SP_DEVICE_INTERFACE_DATA();
                did.cbSize = Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA));

                for (uint i = 0; ; i++)
                {
                    if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref hidGuid, i, ref did)) break;

                    uint need = 0;
                    SetupDiGetDeviceInterfaceDetail(set, ref did, IntPtr.Zero, 0, out need, IntPtr.Zero);
                    if (need == 0) continue;

                    IntPtr buf = Marshal.AllocHGlobal((int)need);
                    try
                    {
                        Marshal.WriteInt32(buf, IntPtr.Size == 8 ? 8 : 6);
                        uint got = 0;
                        if (!SetupDiGetDeviceInterfaceDetail(set, ref did, buf, need, out got, IntPtr.Zero))
                            continue;

                        string path = Marshal.PtrToStringUni(new IntPtr(buf.ToInt64() + 4));
                        if (string.IsNullOrEmpty(path)) continue;

                        HidInterface info = ReadInterface(path);
                        if (info != null) list.Add(info);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(buf);
                    }
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(set);
            }
            return list;
        }

        private static HidInterface ReadInterface(string path)
        {
            IntPtr h = CreateFileW(path, 0, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero,
                OPEN_EXISTING, 0, IntPtr.Zero);
            if (h == new IntPtr(-1)) return null;

            try
            {
                HIDD_ATTRIBUTES attr = new HIDD_ATTRIBUTES();
                attr.Size = Marshal.SizeOf(typeof(HIDD_ATTRIBUTES));
                if (!HidD_GetAttributes(h, ref attr)) return null;

                HIDP_CAPS caps = new HIDP_CAPS();
                caps.Reserved = new ushort[17];
                IntPtr pp;
                if (HidD_GetPreparsedData(h, out pp))
                {
                    HidP_GetCaps(pp, ref caps);
                    HidD_FreePreparsedData(pp);
                }

                HidInterface info = new HidInterface();
                info.Path = path;
                info.Vid = attr.VendorID;
                info.Pid = attr.ProductID;
                info.UsagePage = caps.UsagePage;
                info.Usage = caps.Usage;
                info.InputLength = caps.InputReportByteLength;
                info.OutputLength = caps.OutputReportByteLength;
                info.FeatureLength = caps.FeatureReportByteLength;
                info.Product = SafeString(h, 0);
                info.Manufacturer = SafeString(h, 1);
                info.Serial = SafeString(h, 2);
                return info;
            }
            finally
            {
                CloseHandle(h);
            }
        }

        // =================================================================
        // 句柄封装
        // =================================================================

        public sealed class Handle : IDisposable
        {
            private IntPtr _h = new IntPtr(-1);
            private IntPtr _ev = IntPtr.Zero;
            private readonly object _gate = new object();

            public int LastError = 0;

            public bool IsOpen
            {
                get { return _h != new IntPtr(-1); }
            }

            /// <summary>先按读写打开，失败再退回只读打开。</summary>
            public static Handle Open(string path)
            {
                Handle hd = TryOpen(path, true);
                if (hd != null) return hd;
                return TryOpen(path, false);
            }

            private static Handle TryOpen(string path, bool writeAccess)
            {
                uint access = GENERIC_READ;
                if (writeAccess) access |= GENERIC_WRITE;

                IntPtr h = CreateFileW(path, access, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero,
                    OPEN_EXISTING, FILE_FLAG_OVERLAPPED, IntPtr.Zero);

                if (h == new IntPtr(-1)) return null;

                Handle hd = new Handle();
                hd._h = h;
                hd._ev = CreateEvent(IntPtr.Zero, true, false, IntPtr.Zero);
                if (hd._ev == IntPtr.Zero)
                {
                    CloseHandle(h);
                    return null;
                }
                return hd;
            }

            /// <summary>发一个 Output 报告，然后等一个 Input 报告。null 表示超时或失败。</summary>
            public byte[] Transact(byte[] request, int readLength, int timeoutMs)
            {
                lock (_gate)
                {
                    if (!IsOpen) return null;

                    System.Threading.NativeOverlapped ov = new System.Threading.NativeOverlapped();
                    ResetEvent(_ev);
                    ov.EventHandle = _ev;
                    uint written = 0;
                    bool ok = WriteFile(_h, request, (uint)request.Length, out written, ref ov);
                    if (!ok)
                    {
                        int err = Marshal.GetLastWin32Error();
                        if (err != ERROR_IO_PENDING)
                        {
                            LastError = err;
                            return null;
                        }
                        if (WaitForSingleObject(_ev, (uint)timeoutMs) != 0)
                        {
                            CancelIo(_h);
                            LastError = -1;
                            return null;
                        }
                        if (!GetOverlappedResult(_h, ref ov, out written, false))
                        {
                            LastError = Marshal.GetLastWin32Error();
                            return null;
                        }
                    }

                    // 给设备一点处理时间再读
                    System.Threading.Thread.Sleep(15);
                    return Read(readLength, timeoutMs);
                }
            }

            public byte[] Read(int readLength, int timeoutMs)
            {
                if (!IsOpen) return null;
                byte[] buf = new byte[readLength + 8];

                System.Threading.NativeOverlapped ov = new System.Threading.NativeOverlapped();
                ResetEvent(_ev);
                ov.EventHandle = _ev;
                uint got = 0;
                bool ok = ReadFile(_h, buf, (uint)readLength, out got, ref ov);
                if (!ok)
                {
                    int err = Marshal.GetLastWin32Error();
                    if (err != ERROR_IO_PENDING)
                    {
                        LastError = err;
                        return null;
                    }
                    if (WaitForSingleObject(_ev, (uint)timeoutMs) != 0)
                    {
                        CancelIo(_h);
                        LastError = -2;
                        return null;
                    }
                    if (!GetOverlappedResult(_h, ref ov, out got, false))
                    {
                        LastError = Marshal.GetLastWin32Error();
                        return null;
                    }
                }

                if (got == 0) return null;
                byte[] result = new byte[got];
                Array.Copy(buf, result, (int)got);
                return result;
            }

            /// <summary>只发一个 Output 报告，不等应答。</summary>
            public bool Write(byte[] request)
            {
                lock (_gate)
                {
                    if (!IsOpen) return false;

                    System.Threading.NativeOverlapped ov = new System.Threading.NativeOverlapped();
                    ResetEvent(_ev);
                    ov.EventHandle = _ev;
                    uint written = 0;
                    bool ok = WriteFile(_h, request, (uint)request.Length, out written, ref ov);
                    if (!ok)
                    {
                        int err = Marshal.GetLastWin32Error();
                        if (err != ERROR_IO_PENDING)
                        {
                            LastError = err;
                            return false;
                        }
                        if (WaitForSingleObject(_ev, 400) != 0)
                        {
                            CancelIo(_h);
                            LastError = -1;
                            return false;
                        }
                        if (!GetOverlappedResult(_h, ref ov, out written, false))
                        {
                            LastError = Marshal.GetLastWin32Error();
                            return false;
                        }
                    }
                    return true;
                }
            }

            /// <summary>
            /// 发一个 Feature 报告。长度必须等于该接口的 FeatureReportByteLength，
            /// 否则 hid.dll 直接返回失败（err 87）。
            /// </summary>
            public bool SetFeature(byte[] buffer)
            {
                lock (_gate)
                {
                    if (!IsOpen) return false;
                    if (!HidD_SetFeature(_h, buffer, (uint)buffer.Length))
                    {
                        LastError = Marshal.GetLastWin32Error();
                        return false;
                    }
                    return true;
                }
            }

            /// <summary>
            /// 取一个 Feature 报告。读之前必须先把 buffer 的第 0 字节设成报告 ID，
            /// 返回的报告会覆盖整个 buffer。
            /// </summary>
            public bool GetFeature(byte[] buffer)
            {
                lock (_gate)
                {
                    if (!IsOpen) return false;
                    if (!HidD_GetFeature(_h, buffer, (uint)buffer.Length))
                    {
                        LastError = Marshal.GetLastWin32Error();
                        return false;
                    }
                    return true;
                }
            }

            /// <summary>
            /// 用 IOCTL 形式的 Output 报告发数据。有些设备不收 WriteFile 的
            /// interrupt OUT，只认这条路（或者反过来）。
            /// </summary>
            public bool SetOutputReport(byte[] buffer)
            {
                lock (_gate)
                {
                    if (!IsOpen) return false;
                    if (!HidD_SetOutputReport(_h, buffer, (uint)buffer.Length))
                    {
                        LastError = Marshal.GetLastWin32Error();
                        return false;
                    }
                    return true;
                }
            }

            /// <summary>用 IOCTL 形式取 Input 报告，不等设备主动推。</summary>
            public byte[] GetInputReport(int readLength)
            {
                lock (_gate)
                {
                    if (!IsOpen) return null;
                    byte[] buffer = new byte[readLength];
                    if (!HidD_GetInputReport(_h, buffer, (uint)readLength))
                    {
                        LastError = Marshal.GetLastWin32Error();
                        return null;
                    }
                    return buffer;
                }
            }

            /// <summary>立刻关闭句柄。关闭会取消所有未完成的 I/O，避免残留读取抢走下一次应答。</summary>
            public void Close()
            {
                Dispose();
            }
            public void Dispose()
            {
                lock (_gate)
                {
                    if (_ev != IntPtr.Zero)
                    {
                        CloseHandle(_ev);
                        _ev = IntPtr.Zero;
                    }
                    if (_h != new IntPtr(-1))
                    {
                        CloseHandle(_h);
                        _h = new IntPtr(-1);
                    }
                }
            }
        }
    }
}
