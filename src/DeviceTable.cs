using System;
using System.Collections.Generic;

namespace Volt
{
    /// <summary>
    /// 一个已知型号。同一台设备用接收器和用数据线时 PID 不同，
    /// 所以两个 PID 都记在这一条里——界面因此永远只显示一行。
    ///
    /// 表里的每一行都能追到来源：见 docs/DEVICES.md 里的出处列。
    /// 没有来源的型号不要往里加，宁可少一行，也不要认错型号。
    /// </summary>
    public sealed class DeviceModel
    {
        public string Key = "";
        public string Name = "";
        public string Kind = "外设";
        public string Vendor = "";
        public ushort Vid;

        /// <summary>
        /// 真无线耳塞（左右耳各一只、另有充电盒），不是头戴式耳机。
        /// 只影响界面图标：耳塞画一只耳塞，头戴式画头戴耳机。
        /// </summary>
        public bool Earbuds;

        /// <summary>
        /// 这台设备在某种连接方式下拿不到电量时的说明。
        /// 空表示"没读到就是没读到"，由扫描器自己写「设备未应答」。
        /// </summary>
        public string Note = "";
        public ushort[] Wireless = new ushort[0];
        public ushort[] Wired = new ushort[0];

        /// <summary>用户在 names.tsv 里写的名字。空表示用表里的。</summary>
        public string Override = "";

        /// <summary>界面上显示的名字。</summary>
        public string DisplayName
        {
            get { return Override.Length > 0 ? Override : Name; }
        }

        public bool Owns(ushort pid)
        {
            for (int i = 0; i < Wireless.Length; i++) if (Wireless[i] == pid) return true;
            for (int i = 0; i < Wired.Length; i++) if (Wired[i] == pid) return true;
            return false;
        }

        public bool IsWired(ushort pid)
        {
            for (int i = 0; i < Wired.Length; i++) if (Wired[i] == pid) return true;
            return false;
        }
    }

    /// <summary>
    /// 型号库。只负责「这个 VID:PID 是哪台设备、叫什么」，
    /// 至于怎么问电量，那是 ProtocolsVendor.cs 的事。
    ///
    /// 设备表与协议分开的好处：加一个新型号只加一行数据，不用碰协议代码。
    /// </summary>
    public static class DeviceTable
    {
        private static List<DeviceModel> _models;

        public static List<DeviceModel> All()
        {
            if (_models != null) return _models;
            List<DeviceModel> m = new List<DeviceModel>();

            // ===========================================================
            // ATK / VXE / VGN / 达尔优 / G-Wolves —— Compx 方案
            // 来源：Fan4Metal/mouse_tray chipset/nordic52.py、nordic54.py
            //       Fan4Metal/ATK_tray models.py
            //       XAVIER0229/GearPower src/Models.cs
            // ===========================================================
            Add(m, "atk-x1-se", "ATK X1 SE", "鼠标", "ATK", 0x373B, W(0x10C9), W(0x1107));
            Add(m, "atk-f1-ultimate", "ATK F1 Ultimate", "鼠标", "ATK", 0x373B, W(0x1031), W(0x102E));
            Add(m, "atk-a9-ultimate", "ATK A9 Ultimate", "鼠标", "ATK", 0x373B, W(0x11D9), W(0x11B6));
            Add(m, "atk-zero", "ATK Zero", "鼠标", "ATK", 0x373B, W(0x1155, 0x124F), W(0x1154));
            Add(m, "vxe-mad-r", "VXE MAD R", "鼠标", "VXE", 0x373B, W(0x104D), W(0x103F));
            Add(m, "vxe-mad-r-plus", "VXE MAD R Major Plus", "鼠标", "VXE", 0x373B, W(0x1040), W(0x104C));
            Add(m, "vxe-r1-se-plus-1k", "VXE R1 SE+ (1K dongle)", "鼠标", "VXE", 0x373B, W(0x1085), new ushort[0]);
            Add(m, "vxe-r1-pro-max", "VXE R1 Pro Max", "鼠标", "VXE", 0x3554, W(0xF58A), W(0xF58C));
            Add(m, "vxe-r1-se-plus", "VXE R1 SE+", "鼠标", "VXE", 0x3554, W(0xF58E), W(0xF58F));
            Add(m, "vgn-f1-pro", "VGN F1 Pro", "鼠标", "VGN", 0x3554, W(0xF503), W(0xF502));
            Add(m, "vgn-f2-pro-max", "VGN F2 Pro Max", "鼠标", "VGN", 0x3554, W(0xFB3E), W(0xFB3D));
            Add(m, "zaopin-z2-mini", "Zaopin Z2 Mini", "鼠标", "Zaopin", 0x3554, W(0xF524), W(0xF526));
            Add(m, "scyrox-v8", "Scyrox V8", "鼠标", "Scyrox", 0x3554, W(0xF5F7), W(0xF5F6));
            Add(m, "dareu-a950-air", "Dareu A950 Air", "鼠标", "Dareu", 0x260D, W(0x1175), W(0x1193));
            Add(m, "dareu-a950-wing", "Dareu A950 Wing", "鼠标", "Dareu", 0x260D, W(0x1154), W(0x1196));
            Add(m, "gwolves-lycan", "G-Wolves Lycan", "鼠标", "G-Wolves", 0x33E4, W(0x3854), W(0x4719));

            // ===========================================================
            // 狼蛛 AULA / LEOBOG —— 20 字节帧
            // 来源：brhnshn/AulaTray ModelRegistry.cs、XAVIER0229/GearPower
            //
            // 0xFA09 是通用接收器 PID，F75、F99 和 LEOBOG 的几款键盘都用它，
            // 光看 VID:PID 分不出是哪一把。所以这里写成「F75 / F99」，
            // 而不是随便挑一个型号——认错型号比认不出型号更糟。
            // 想知道自己到底是哪一把，在 names.tsv 里写一行就固定下来。
            // ===========================================================
            // LEOBOG Hi75c Pro 与 HI98PRO 用的是同一个通用接收器 PID，
            // 光看 VID:PID 与 AULA 那两把分不开，所以并进同一行，
            // 不再单列——列了也永远匹配不到（Lookup 取第一个命中）。
            Add(m, "aula-f75-f99", "AULA F75 / F99", "键盘", "AULA", 0x3554, W(0xFA09), new ushort[0]);
            Add(m, "aula-f87", "AULA F87", "键盘", "AULA", 0x3554, W(0xFA0A, 0xFA10), W(0x010D));
            Add(m, "aula-f68", "AULA F68", "键盘", "AULA", 0x3554, W(0xFA08), W(0x010B));
            Add(m, "aula-wired", "AULA (wired)", "键盘", "AULA", 0x258A, new ushort[0], W(0x010B, 0x010C, 0x010D, 0x010E));

            // ===========================================================
            // Razer 鼠标 —— 91 字节 Feature 报告
            // 来源：openrazer or_mouse.py（哪些型号支持 get_battery）
            //       XAVIER0229/GearPower src/Models.cs（无线/有线 PID 配对）
            // ===========================================================
            Add(m, "razer-viper-v2-pro", "Razer Viper V2 Pro", "鼠标", "Razer", 0x1532, W(0x00A6), W(0x00A5));
            Add(m, "razer-viper-v3-pro", "Razer Viper V3 Pro", "鼠标", "Razer", 0x1532, W(0x00C1), W(0x00C0));
            Add(m, "razer-viper-v3-pro-se", "Razer Viper V3 Pro SE", "鼠标", "Razer", 0x1532, W(0x00DD), W(0x00DE));
            Add(m, "razer-viper-v3-hyperspeed", "Razer Viper V3 HyperSpeed", "鼠标", "Razer", 0x1532, W(0x00B8), new ushort[0]);
            Add(m, "razer-deathadder-v3-pro", "Razer DeathAdder V3 Pro", "鼠标", "Razer", 0x1532, W(0x00B7), W(0x00B6));
            Add(m, "razer-deathadder-v4-pro", "Razer DeathAdder V4 Pro", "鼠标", "Razer", 0x1532, W(0x00BF), W(0x00BE));
            Add(m, "razer-deathadder-v2-pro", "Razer DeathAdder V2 Pro", "鼠标", "Razer", 0x1532, W(0x007D), W(0x007C));
            Add(m, "razer-deathadder-v2-x", "Razer DeathAdder V2 X HyperSpeed", "鼠标", "Razer", 0x1532, W(0x009C), new ushort[0]);
            Add(m, "razer-deathadder-v3-hs", "Razer DeathAdder V3 HyperSpeed", "鼠标", "Razer", 0x1532, W(0x00C5), W(0x00C4));
            Add(m, "razer-basilisk-v3-pro", "Razer Basilisk V3 Pro", "鼠标", "Razer", 0x1532, W(0x00AB), W(0x00AA));
            Add(m, "razer-basilisk-v3-pro-35k", "Razer Basilisk V3 Pro 35K", "鼠标", "Razer", 0x1532, W(0x00CD), W(0x00CC));
            Add(m, "razer-basilisk-v3-pro-35k-pg", "Razer Basilisk V3 Pro 35K Phantom Green", "鼠标", "Razer", 0x1532, W(0x00D7), W(0x00D6));
            Add(m, "razer-basilisk-ultimate", "Razer Basilisk Ultimate", "鼠标", "Razer", 0x1532, W(0x0088), W(0x0086));
            Add(m, "razer-basilisk-x-hs", "Razer Basilisk X HyperSpeed", "鼠标", "Razer", 0x1532, W(0x0083), new ushort[0]);
            Add(m, "razer-basilisk-v3-x-hs", "Razer Basilisk V3 X HyperSpeed", "鼠标", "Razer", 0x1532, W(0x00B9), new ushort[0]);
            Add(m, "razer-basilisk-mobile", "Razer Basilisk Mobile", "鼠标", "Razer", 0x1532, W(0x00D3), new ushort[0]);
            Add(m, "razer-cobra-pro", "Razer Cobra Pro", "鼠标", "Razer", 0x1532, W(0x00B0), W(0x00AF));
            Add(m, "razer-cobra-hs", "Razer Cobra HyperSpeed", "鼠标", "Razer", 0x1532, W(0x00DA), new ushort[0]);
            Add(m, "razer-naga-pro", "Razer Naga Pro", "鼠标", "Razer", 0x1532, W(0x0090), W(0x008F));
            Add(m, "razer-naga-v2-pro", "Razer Naga V2 Pro", "鼠标", "Razer", 0x1532, W(0x00A8), W(0x00A7));
            Add(m, "razer-naga-v2-hs", "Razer Naga V2 HyperSpeed", "鼠标", "Razer", 0x1532, W(0x00B4), new ushort[0]);
            Add(m, "razer-naga-epic", "Razer Naga Epic", "鼠标", "Razer", 0x1532, W(0x001F), new ushort[0]);
            Add(m, "razer-naga-epic-chroma", "Razer Naga Epic Chroma", "鼠标", "Razer", 0x1532, W(0x003E), new ushort[0]);
            Add(m, "razer-lancehead", "Razer Lancehead Wireless", "鼠标", "Razer", 0x1532, W(0x0070), W(0x0059));
            Add(m, "razer-mamba-chroma", "Razer Mamba Chroma Wireless", "鼠标", "Razer", 0x1532, W(0x0045), W(0x0044));
            Add(m, "razer-mamba-2012", "Razer Mamba 2012", "鼠标", "Razer", 0x1532, W(0x0025), W(0x0024));
            Add(m, "razer-mamba-wireless", "Razer Mamba Wireless", "鼠标", "Razer", 0x1532, W(0x0073), new ushort[0]);
            Add(m, "razer-ouroboros", "Razer Ouroboros", "鼠标", "Razer", 0x1532, W(0x0032), new ushort[0]);
            Add(m, "razer-viper-ultimate", "Razer Viper Ultimate", "鼠标", "Razer", 0x1532, W(0x0078), W(0x007A));
            Add(m, "razer-viper-mini-se", "Razer Viper Mini SE", "鼠标", "Razer", 0x1532, W(0x009E), new ushort[0]);
            Add(m, "razer-atheris", "Razer Atheris", "鼠标", "Razer", 0x1532, W(0x0062), new ushort[0]);
            Add(m, "razer-orochi-v2", "Razer Orochi V2", "鼠标", "Razer", 0x1532, W(0x0094), new ushort[0]);
            Add(m, "razer-pro-click", "Razer Pro Click", "鼠标", "Razer", 0x1532, W(0x0077), new ushort[0]);
            Add(m, "razer-pro-click-mini", "Razer Pro Click Mini", "鼠标", "Razer", 0x1532, W(0x009A), new ushort[0]);
            Add(m, "razer-pro-click-v2", "Razer Pro Click V2", "鼠标", "Razer", 0x1532, W(0x00D1), W(0x00D0));
            Add(m, "razer-pro-click-v2-vertical", "Razer Pro Click V2 Vertical Edition", "鼠标", "Razer", 0x1532, W(0x00C8), W(0x00C7));
            Add(m, "razer-hyperpolling-dongle", "Razer HyperPolling Dongle", "接收器", "Razer", 0x1532, W(0x00B3), new ushort[0]);

            // Razer 键盘（openrazer 里支持 get_battery 的 8 个）
            Add(m, "razer-blackwidow-v3-pro", "Razer BlackWidow V3 Pro", "键盘", "Razer", 0x1532, W(0x025C), W(0x025A));
            Add(m, "razer-blackwidow-v3-mini", "Razer BlackWidow V3 Mini HyperSpeed", "键盘", "Razer", 0x1532, W(0x0259), W(0x0258));
            Add(m, "razer-blackwidow-v4-tkl", "Razer BlackWidow V4 TKL HyperSpeed", "键盘", "Razer", 0x1532, W(0x02D6), W(0x02D7));
            Add(m, "razer-blackwidow-v4-mini", "Razer BlackWidow V4 Mini HyperSpeed", "键盘", "Razer", 0x1532, W(0x02B8), W(0x02B9));
            Add(m, "razer-deathstalker-v2-pro", "Razer DeathStalker V2 Pro", "键盘", "Razer", 0x1532, W(0x0293), W(0x0292));
            Add(m, "razer-deathstalker-v2-pro-tkl", "Razer DeathStalker V2 Pro TKL", "键盘", "Razer", 0x1532, W(0x0299), W(0x0298));
            Add(m, "razer-joro", "Razer Joro", "键盘", "Razer", 0x1532, W(0x02CD), new ushort[0]);

            // Razer 耳机
            Add(m, "razer-blackshark-v2-pro-24", "Razer BlackShark V2 Pro (2.4G)", "耳机", "Razer", 0x1532, W(0x0555), new ushort[0]);

            // ===========================================================
            // 罗技 —— HID++ 2.0。一个接收器上可以挂好几台设备，
            // 型号在运行时从设备里读出来，所以这里只列接收器。
            // 来源：Solaar lib/logitech_receiver/base_usb.py 的 KNOWN_RECEIVERS
            // ===========================================================
            Add(m, "logitech-bolt", "Logitech Bolt 接收器", "接收器", "Logitech", 0x046D, W(0xC548), new ushort[0]);
            Add(m, "logitech-unifying", "Logitech Unifying 接收器", "接收器", "Logitech", 0x046D, W(0xC52B, 0xC532), new ushort[0]);
            Add(m, "logitech-nano", "Logitech Nano 接收器", "接收器", "Logitech", 0x046D,
                W(0xC52F, 0xC518, 0xC51A, 0xC51B, 0xC521, 0xC525, 0xC526, 0xC52E, 0xC531, 0xC534, 0xC535, 0xC537),
                new ushort[0]);
            // 0xC547 是 PRO X Wireless 耳机（不是鼠标接收器），单独一行，这里不重复列。
            Add(m, "logitech-lightspeed", "Logitech Lightspeed 接收器", "接收器", "Logitech", 0x046D,
                W(0xC539, 0xC53A, 0xC53D, 0xC53F, 0xC541, 0xC545, 0xC54D), new ushort[0]);
            Add(m, "logitech-lenovo", "Lenovo Nano 接收器", "接收器", "Lenovo", 0x17EF, W(0x6042), new ushort[0]);
            Add(m, "logitech-ex100", "Logitech EX100 接收器", "接收器", "Logitech", 0x046D, W(0xC517), new ushort[0]);
            Add(m, "logitech-pro-x-wireless", "Logitech PRO X Wireless", "耳机", "Logitech", 0x046D, W(0xC547), new ushort[0]);

            // ===========================================================
            // HyperX 耳机 —— 20 字节 21 FF 05 帧
            // 来源：XAVIER0229/GearPower src/Protocols.cs HyperXProtocol
            // ===========================================================
            Add(m, "hyperx-cloud-alpha-wl", "HyperX Cloud Alpha Wireless", "耳机", "HyperX", 0x03F0, W(0x098D), new ushort[0]);
            Add(m, "hyperx-cloud-iii-wl", "HyperX Cloud III Wireless", "耳机", "HyperX", 0x03F0, W(0x05B7), new ushort[0]);
            Add(m, "hyperx-cloud-flight", "HyperX Cloud Flight Wireless", "耳机", "HyperX", 0x03F0, W(0x0E90, 0x1723, 0x1749, 0x16C4), new ushort[0]);
            Add(m, "hyperx-cloud-flight-0951", "HyperX Cloud Flight Wireless", "耳机", "HyperX", 0x0951, W(0x1723), new ushort[0]);

            // ===========================================================
            // 华硕 ROG —— 6 字节 Feature 报告
            // 来源：XAVIER0229/GearPower src/Protocols.cs AsusProtocol
            // ===========================================================
            Add(m, "asus-rog-pelta", "ASUS ROG PELTA", "耳机", "ASUS ROG", 0x0B05, W(0x1B84), new ushort[0]);
            Add(m, "asus-rog-falchion", "ASUS ROG Falchion", "键盘", "ASUS ROG", 0x0B05, W(0x193E), new ushort[0]);

            // ===========================================================
            // MCHOSE —— 0xFF01 异或编码
            // 来源：Fan4Metal/mouse_tray chipset/realtek.py、GearPower MchoseProtocol
            // ===========================================================
            Add(m, "mchose-l7-pro", "MCHOSE L7 Pro", "鼠标", "MCHOSE", 0x5253, W(0x1020), W(0x00B0));
            Add(m, "mchose-k7-ultra", "MCHOSE K7 Ultra", "鼠标", "MCHOSE", 0x3837, W(0x1001), W(0x4150));

            // ===========================================================
            // 耳机 —— 型号表来源：Sapd/HeadsetControl lib/devices/*.hpp
            // 这里的每个型号在那边的 getVendorId()/getProductIds() 里都能查到，
            // 协议实现在 ProtocolsVendor.cs 里按品牌一一对应。
            // ===========================================================

            // Audeze（Airoha 平台，62 字节 0x06 写 / 0x07 读）
            Add(m, "audeze-maxwell", "Audeze Maxwell", "耳机", "Audeze", 0x3329, W(0x4B19), W(0x4B18));
            Add(m, "audeze-maxwell-2", "Audeze Maxwell 2", "耳机", "Audeze", 0x3329, W(0x4B29), W(0x4B28));

            // Corsair（Virtuoso 走 0xFF42，Void 走 0xC9 0x64 两字节命令）
            Add(m, "corsair-virtuoso-xt", "Corsair Virtuoso XT", "耳机", "Corsair", 0x1B1C, W(0x0A64), W(0x0A62));
            Add(m, "corsair-virtuoso-se", "Corsair Virtuoso SE", "耳机", "Corsair", 0x1B1C, W(0x0A3E), W(0x0A3D));
            Add(m, "corsair-void", "Corsair Void", "耳机", "Corsair", 0x1B1C,
                W(0x0A14, 0x0A16, 0x0A17, 0x0A1A, 0x0A1D, 0x0A2B, 0x0A38, 0x0A4F, 0x0A51, 0x0A52,
                  0x0A55, 0x0A56, 0x0A75, 0x1B23, 0x1B27, 0x1B29, 0x1B2A), new ushort[0]);
            Add(m, "corsair-void-v2", "Corsair Void V2", "耳机", "Corsair", 0x1B1C, W(0x2A02, 0x2A08), new ushort[0]);

            // Jabra（GNP 64 字节，report id 0x05）
            Add(m, "jabra-evolve2-65-flex", "Jabra Evolve2 65 Flex", "耳机", "Jabra", 0x0B0E, W(0x2519), new ushort[0]);
            Add(m, "jabra-link-380", "Jabra Link 380", "耳机", "Jabra", 0x0B0E, W(0x24C7), new ushort[0]);
            Add(m, "jabra-link-390", "Jabra Link 390", "耳机", "Jabra", 0x0B0E, W(0x2E57), new ushort[0]);

            // Lenovo（61 字节，电量在 [7]）
            Add(m, "lenovo-wireless-voip", "Lenovo Wireless VoIP", "耳机", "Lenovo", 0x17EF, W(0xA07D), new ushort[0]);

            // Plantronics（64 字节，report id 0x07）
            Add(m, "plantronics-bt600", "Plantronics BT600", "耳机", "Plantronics", 0x047F, W(0x02EE), new ushort[0]);

            // SteelSeries（Legacy 31 字节 / Nova 64 字节两套）
            Add(m, "steelseries-arctis-1", "SteelSeries Arctis 1", "耳机", "SteelSeries", 0x1038,
                W(0x12B3, 0x12B6, 0x12D7, 0x12D5), new ushort[0]);
            Add(m, "steelseries-arctis-7", "SteelSeries Arctis 7", "耳机", "SteelSeries", 0x1038,
                W(0x1260, 0x12AD, 0x1252, 0x1280), new ushort[0]);
            Add(m, "steelseries-arctis-7-plus", "SteelSeries Arctis 7+", "耳机", "SteelSeries", 0x1038,
                W(0x220E, 0x2212, 0x2216, 0x2236), new ushort[0]);
            Add(m, "steelseries-arctis-9", "SteelSeries Arctis 9", "耳机", "SteelSeries", 0x1038,
                W(0x12C2, 0x12C4), new ushort[0]);
            Add(m, "steelseries-arctis-pro-wl", "SteelSeries Arctis Pro Wireless", "耳机", "SteelSeries", 0x1038,
                W(0x1290), new ushort[0]);
            Add(m, "steelseries-gamebuds", "SteelSeries Arctis GameBuds", "耳机", "SteelSeries", 0x1038,
                W(0x230A), new ushort[0], true);
            Add(m, "steelseries-nova-3", "SteelSeries Arctis Nova 3", "耳机", "SteelSeries", 0x1038,
                W(0x12EC), new ushort[0]);
            Add(m, "steelseries-nova-3p", "SteelSeries Arctis Nova 3P", "耳机", "SteelSeries", 0x1038,
                W(0x2269, 0x226D), new ushort[0]);
            Add(m, "steelseries-nova-5", "SteelSeries Arctis Nova 5", "耳机", "SteelSeries", 0x1038,
                W(0x2232, 0x2253), new ushort[0]);
            Add(m, "steelseries-nova-7", "SteelSeries Arctis Nova 7", "耳机", "SteelSeries", 0x1038,
                W(0x2202, 0x2206, 0x223A, 0x2258, 0x227A, 0x227E, 0x229E, 0x22A1, 0x22A4, 0x22A5,
                  0x22A9, 0x22AD), new ushort[0]);
            Add(m, "steelseries-nova-7p", "SteelSeries Arctis Nova 7P", "耳机", "SteelSeries", 0x1038,
                W(0x220A, 0x2298, 0x22A7), new ushort[0]);
            Add(m, "steelseries-nova-pro-wl", "SteelSeries Arctis Nova Pro Wireless", "耳机", "SteelSeries", 0x1038,
                W(0x12E0, 0x12E5), new ushort[0]);

            // Sony（INZONE Buds 是唯一有完整左/右/盒三路字节的开源实现）
            Add(m, "sony-inzone-buds", "Sony INZONE Buds", "耳机", "Sony", 0x054C, W(0x0EC2), new ushort[0], true);
            Add(m, "sony-inzone-h5", "Sony INZONE H5", "耳机", "Sony", 0x054C, W(0x0EBF), new ushort[0]);
            Add(m, "sony-inzone-h9-2", "Sony INZONE H9 II", "耳机", "Sony", 0x054C, W(0x0FA8), new ushort[0]);
            Add(m, "sony-pulse-3d", "Sony PULSE 3D", "耳机", "Sony", 0x054C, W(0x0D5E), new ushort[0]);

            // Logitech 耳机（HID++ 或 Centurion 协议）
            Add(m, "logitech-astro-a50", "ASTRO A50 Gen 5", "耳机", "Logitech", 0x046D, W(0x0B1C), new ushort[0]);
            Add(m, "logitech-astro-a50-gen4", "ASTRO A50 Gen 4", "耳机", "ASTRO", 0x9886, W(0x002C), new ushort[0]);
            Add(m, "logitech-g431-g433", "Logitech G431/G432/G433", "耳机", "Logitech", 0x046D,
                W(0x0A9B, 0x0A9C, 0x0A6D), new ushort[0]);
            Add(m, "logitech-g522", "Logitech G522 LIGHTSPEED", "耳机", "Logitech", 0x046D, W(0x0B18), new ushort[0]);
            Add(m, "logitech-g533", "Logitech G533", "耳机", "Logitech", 0x046D, W(0x0A66), new ushort[0]);
            Add(m, "logitech-g535", "Logitech G535", "耳机", "Logitech", 0x046D, W(0x0AC4), new ushort[0]);
            Add(m, "logitech-g633-g935", "Logitech G633/G733/G933/G935", "耳机", "Logitech", 0x046D,
                W(0x0A5C, 0x0A89, 0x0A5B, 0x0A87, 0x0AB5, 0x0AFE, 0x0B1F), new ushort[0]);
            Add(m, "logitech-g930", "Logitech G930", "耳机", "Logitech", 0x046D, W(0x0A1F), new ushort[0]);
            Add(m, "logitech-g-pro-headset", "Logitech G PRO Series", "耳机", "Logitech", 0x046D,
                W(0x0AA7, 0x0AAA, 0x0ABA, 0x0AFB, 0x0AFC), new ushort[0]);
            Add(m, "logitech-g-pro-x2", "Logitech G PRO X 2 LIGHTSPEED", "耳机", "Logitech", 0x046D, W(0x0AF7), new ushort[0]);
            Add(m, "logitech-zone-wired", "Logitech Zone Wired", "耳机", "Logitech", 0x046D, W(0x0AAD, 0x0ADE), new ushort[0]);

            // HyperX 其它型号（Cloud II / Cloud 3 / Alpha 2 / Alpha S）
            Add(m, "hyperx-cloud-2-wl", "HyperX Cloud II Wireless", "耳机", "HyperX", 0x03F0, W(0x0696), new ushort[0]);
            Add(m, "hyperx-cloud-2-wl-kingston", "HyperX Cloud II Wireless (Kingston)", "耳机", "HyperX", 0x0951, W(0x1718), new ushort[0]);
            Add(m, "hyperx-cloud-3", "HyperX Cloud III", "耳机", "HyperX", 0x03F0, W(0x089D, 0x03CC), new ushort[0]);
            Add(m, "hyperx-cloud-alpha-2-wl", "HyperX Cloud Alpha 2 Wireless", "耳机", "HyperX", 0x03F0, W(0x08BE), new ushort[0]);
            Add(m, "hyperx-cloud-alpha-s", "HyperX Cloud Alpha S", "耳机", "HyperX", 0x0951, W(0x16ED), new ushort[0]);

            // MCHOSE 耳机（0xFF90 / report id 0x55）
            Add(m, "mchose-x9", "MCHOSE X9", "耳机", "MCHOSE", 0x3837, W(0x6045), new ushort[0]);
            Add(m, "mchose-v9-pro", "MCHOSE V9 Pro", "耳机", "MCHOSE", 0x291D, W(0x385D), new ushort[0]);

            // Razer 耳机（BlackShark V2 Pro PS 版，0x0E 0xE6 链路查询 + 0x06 0x31 电压）
            Add(m, "razer-blackshark-v2-pro-ps", "Razer BlackShark V2 Pro (PS)", "耳机", "Razer", 0x1532,
                W(0x1014), new ushort[0]);

            // ===========================================================
            // 其它
            // ===========================================================
            Add(m, "bloody-sg5-pro", "Bloody SG5 Pro", "鼠标", "A4Tech", 0x1956, W(0x3069), W(0x3144));
            Add(m, "ninjutso-sora-v2", "Ninjutso Sora V2", "鼠标", "Ninjutso", 0x1915, W(0xAE1C), W(0xAE11));
            Add(m, "lamzu-maya-x", "Lamzu Maya X", "鼠标", "Lamzu", 0x373E, W(0x001E), W(0x001C));
            Add(m, "lamzu-inca", "Lamzu Inca", "鼠标", "Lamzu", 0x37B0, W(0x0010), W(0x0009));
            Add(m, "lamzu-thorn-v2", "Lamzu Thorn V2", "鼠标", "Lamzu", 0x37B0, W(0x0040), new ushort[0]);
            Add(m, "attackshark-x3", "Attack Shark X3", "鼠标", "Attack Shark", 0x1D57, W(0xFA60), W(0xFA61));
            Add(m, "flydigi-vader-5-pro", "Flydigi Vader 5 Pro", "手柄", "Flydigi", 0x37D7, W(0x2401), new ushort[0]);
            Add(m, "ninx-nx60", "NINX NX60 ANSI", "键盘", "NINX", 0xCAFE, W(0x910B), new ushort[0]);
            Add(m, "dualsense", "Sony DualSense", "手柄", "Sony", 0x054C, W(0x0CE6), new ushort[0]);
            Add(m, "dualshock-4", "Sony DualShock 4", "手柄", "Sony", 0x054C, W(0x09CC, 0x05C4), new ushort[0]);
            // 这台只走 2.4G 接收器，而 Actions 的接收器不通过 HID 上报电量：
            // 0x55 / 0x41 是 ADFU 引导模式触发器（写进去会把接收器切成 10d6:10d6，
            // 要拔掉重插才恢复），0x06 / 0x07 是音量与静音 LED。
            // 证据见 docs/PROTOCOLS.md。末位那句说明的用处是：扫描器看到「有说明」
            // 且「本次没读到值」，就知道这台设备在这条连接上永远读不到，直接不列出来，
            // 免得界面上长期挂一行空行。哪天真读到了（换了连接方式、补了协议），
            // 它会自己出现。
            Add(m, "baseus-aequr-g10", "Baseus AeQur G10", "耳机", "Baseus", 0x10D6, W(0x4801), new ushort[0],
                true, "2.4G 接收器不上报电量");

            _models = m;
            ApplyOverrides(_models);
            return _models;
        }

        /// <summary>
        /// 用户自己的改名表：%LOCALAPPDATA%\Volt\names.tsv，每行「key<TAB>名字」。
        ///
        /// 为什么需要它：有些接收器 PID 是通用的，光看 VID:PID 分不出具体型号
        /// （比如狼蛛的 0xFA09，F75 和 F99 都用它）。表里只能写一个折中的名字，
        /// 而用户自己知道插的是哪一把。写一行就固定下来，改完不用重启。
        /// </summary>
        private static void ApplyOverrides(List<DeviceModel> models)
        {
            // 先把上一轮的覆盖清掉，否则用户删掉一行，名字还留在内存里。
            for (int i = 0; i < models.Count; i++) models[i].Override = "";
            try
            {
                string file = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Volt", "names.tsv");
                if (!System.IO.File.Exists(file)) return;

                Dictionary<string, string> map = new Dictionary<string, string>();
                foreach (string line in System.IO.File.ReadAllLines(file))
                {
                    if (line.Length == 0 || line[0] == '#') continue;
                    int tab = line.IndexOf('\t');
                    if (tab <= 0) continue;
                    string key = line.Substring(0, tab).Trim();
                    string name = line.Substring(tab + 1).Trim();
                    if (key.Length == 0 || name.Length == 0) continue;
                    map[key] = name;
                }

                for (int i = 0; i < models.Count; i++)
                {
                    string name;
                    if (map.TryGetValue(models[i].Key, out name)) models[i].Override = name;
                }
            }
            catch { }
        }

        /// <summary>按 VID:PID 找型号。找不到返回 null（下面还有厂商通配兜底）。</summary>
        public static DeviceModel Lookup(ushort vid, ushort pid)
        {
            List<DeviceModel> all = All();
            for (int i = 0; i < all.Count; i++)
            {
                DeviceModel m = all[i];
                if (m.Vid == vid && m.Owns(pid)) return m;
            }
            return null;
        }

        /// <summary>
        /// 表里没有的型号：只要能认出厂商，就给一个不会认错的临时名字。
        /// 名字里带 VID:PID，用户一看就知道这台设备还没登记进表。
        /// </summary>
        /// <param name="allowUnknownVendor">
        /// 厂商名都不认识时是否也给个条目。只有「设备自带标准 HID 电量」这种情况才传 true——
        /// 那种设备不需要任何私有协议，不登记也能读，没必要因为厂商名不在表里就丢掉。
        /// </param>
        public static DeviceModel Fallback(ushort vid, ushort pid, string product, bool allowUnknownVendor)
        {
            string vendor = VendorName(vid);
            if (vendor.Length == 0)
            {
                if (!allowUnknownVendor) return null;
                vendor = vid.ToString("X4") + ":" + pid.ToString("X4");
            }

            DeviceModel m = new DeviceModel();
            m.Key = "auto-" + vid.ToString("X4") + "-" + pid.ToString("X4");
            m.Vendor = vendor;
            m.Vid = vid;
            m.Wireless = new ushort[] { pid };
            m.Kind = GuessKind(product);
            string cleaned = CleanProduct(product);
            if (cleaned.Length > 0) m.Name = vendor + " " + cleaned;
            else m.Name = vendor + " " + vid.ToString("X4") + ":" + pid.ToString("X4");
            return m;
        }

        /// <summary>厂商名。只写有把握的，不确定就留空。</summary>
        public static string VendorName(ushort vid)
        {
            switch (vid)
            {
                case 0x373B: return "ATK";
                case 0x3554: return "VXE / VGN";
                case 0x260D: return "Dareu";
                case 0x33E4: return "G-Wolves";
                case 0x373E: return "Lamzu";
                case 0x37B0: return "Lamzu";
                case 0x1532: return "Razer";
                case 0x0B05: return "ASUS ROG";
                case 0x03F0: return "HyperX";
                case 0x0951: return "HyperX";
                case 0x046D: return "Logitech";
                case 0x17EF: return "Lenovo";
                case 0x1D57: return "Attack Shark";
                case 0x5253: return "MCHOSE";
                case 0x3837: return "MCHOSE";
                case 0x1956: return "A4Tech";
                case 0x1915: return "Ninjutso";
                case 0x3329: return "Audeze";
                case 0x1B1C: return "Corsair";
                case 0x0B0E: return "Jabra";
                case 0x047F: return "Plantronics";
                case 0x1038: return "SteelSeries";
                case 0x9886: return "ASTRO";
                case 0x291D: return "MCHOSE";
                case 0x37D7: return "Flydigi";
                case 0x054C: return "Sony";
                case 0x10D6: return "Baseus";
                case 0x258A: return "AULA";
                case 0x0C45: return "Compx";
                default: return "";
            }
        }

        private static string GuessKind(string product)
        {
            if (product == null) return "外设";
            string p = product.ToLowerInvariant();
            if (p.IndexOf("keyboard") >= 0 || p.IndexOf("keyb") >= 0) return "键盘";
            if (p.IndexOf("mouse") >= 0) return "鼠标";
            if (p.IndexOf("headset") >= 0 || p.IndexOf("headphone") >= 0) return "耳机";
            if (p.IndexOf("gamepad") >= 0 || p.IndexOf("controller") >= 0) return "手柄";
            if (p.IndexOf("receiver") >= 0 || p.IndexOf("dongle") >= 0) return "接收器";
            return "外设";
        }

        /// <summary>接收器的产品名对用户没意义（「2.4G Wireless Receiver」），去掉。</summary>
        private static string CleanProduct(string product)
        {
            if (string.IsNullOrEmpty(product)) return "";
            string p = product.Trim();
            string lower = p.ToLowerInvariant();
            if (lower == "usb receiver" || lower == "2.4g wireless receiver" || lower == "receiver"
                || lower == "wireless receiver" || lower == "compx" || lower == "hid-compliant mouse"
                || lower == "hid-compliant keyboard" || lower == "usb composite device"
                || lower == "2.4g receiver" || lower == "wireless device") return "";
            return p;
        }

        private static ushort[] W(params int[] values)
        {
            ushort[] r = new ushort[values.Length];
            for (int i = 0; i < values.Length; i++) r[i] = (ushort)values[i];
            return r;
        }

        private static void Add(List<DeviceModel> list, string key, string name, string kind,
            string vendor, int vid, ushort[] wireless, ushort[] wired,
            bool earbuds = false, string note = "")
        {
            DeviceModel m = new DeviceModel();
            m.Key = key;
            m.Name = name;
            m.Kind = kind;
            m.Vendor = vendor;
            m.Vid = (ushort)vid;
            m.Wireless = wireless;
            m.Wired = wired;
            m.Earbuds = earbuds;
            m.Note = note;
            list.Add(m);
        }
    }
}
