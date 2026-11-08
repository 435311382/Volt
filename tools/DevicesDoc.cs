using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Volt;

// Emits docs/DEVICES.md straight from DeviceTable.All(), so the published table
// can never drift from the code that actually does the lookup.
public static class DevicesDoc
{
    // Models that were exercised against real hardware on the development machine.
    private static readonly string[] Verified = { "atk-x1-se", "aula-f75-f99" };

    public static int Main(string[] args)
    {
        List<DeviceModel> all = DeviceTable.All();
        all.Sort(delegate(DeviceModel a, DeviceModel b)
        {
            int v = string.Compare(a.Vendor, b.Vendor, StringComparison.Ordinal);
            if (v != 0) return v;
            return string.Compare(a.Name, b.Name, StringComparison.Ordinal);
        });

        int wireless = 0, wired = 0, both = 0;
        Dictionary<string, int> vendorCount = new Dictionary<string, int>();
        Dictionary<string, int> kindCount = new Dictionary<string, int>();
        for (int i = 0; i < all.Count; i++)
        {
            DeviceModel m = all[i];
            if (vendorCount.ContainsKey(m.Vendor)) vendorCount[m.Vendor]++; else vendorCount[m.Vendor] = 1;
            if (kindCount.ContainsKey(KindName(m.Kind))) kindCount[KindName(m.Kind)]++; else kindCount[KindName(m.Kind)] = 1;
            int w = m.Wireless == null ? 0 : m.Wireless.Length;
            int c = m.Wired == null ? 0 : m.Wired.Length;
            wireless += w;
            wired += c;
            if (w > 0 && c > 0) both++;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("# Device list");
        sb.AppendLine();
        sb.AppendLine("Generated from `src/DeviceTable.cs`. Do not edit by hand.");
        sb.AppendLine();
        sb.AppendLine("**" + all.Count + " models, " + vendorCount.Count + " vendors, "
            + wireless + " wireless PIDs, " + wired + " wired PIDs.** "
            + both + " models expose both a dongle and a cable.");
        sb.AppendLine();
        sb.AppendLine("| Kind | Models |");
        sb.AppendLine("|---|---|");
        AppendSorted(sb, kindCount);
        sb.AppendLine();
        sb.AppendLine("| Vendor | Models |");
        sb.AppendLine("|---|---|");
        AppendSorted(sb, vendorCount);        sb.AppendLine();
        sb.AppendLine("## What \"supported\" means here");
        sb.AppendLine();
        sb.AppendLine("A row means Volt knows the VID:PID and has a protocol implementation for that family. "
            + "Two levels of confidence:");
        sb.AppendLine();
        sb.AppendLine("- **Verified** — read against real hardware on the development machine.");
        sb.AppendLine("- **Ported** — frame format taken from an open-source implementation or vendor documentation. "
            + "The protocol family is implemented; the individual model has not been tested.");
        sb.AppendLine();
        sb.AppendLine("A model that reports through the standard HID battery page (`0x0006` / `0x0020`) "
            + "works without appearing in this table at all.");
        sb.AppendLine();
        sb.AppendLine("**Wireless** lists the product IDs the device uses on a 2.4G or Bluetooth dongle; "
            + "**Cable** lists the IDs it uses when plugged in. A model with both appears once in the flyout.");
        sb.AppendLine();

        string currentVendor = null;
        for (int i = 0; i < all.Count; i++)
        {
            DeviceModel m = all[i];
            if (m.Vendor != currentVendor)
            {
                if (currentVendor != null) sb.AppendLine();
                currentVendor = m.Vendor;
                sb.AppendLine("## " + m.Vendor);
                sb.AppendLine();
                sb.AppendLine("| Model | Kind | Wireless | Cable | Status |");
                sb.AppendLine("|---|---|---|---|---|");
            }
            sb.Append("| ").Append(Escape(m.DisplayName))
              .Append(" | ").Append(KindName(m.Kind))
              .Append(" | ").Append(Pids(m.Wireless))
              .Append(" | ").Append(Pids(m.Wired))
              .Append(" | ").Append(IsVerified(m) ? "Verified" : "Ported")
              .AppendLine(" |");
        }

        string text = sb.ToString();
        if (args.Length > 0)
        {
            File.WriteAllText(args[0], text, new UTF8Encoding(false));
            Console.WriteLine("wrote " + args[0] + " (" + text.Length + " chars)");
        }
        else
        {
            Console.Write(text);
        }
        return 0;
    }

    private static void AppendSorted(StringBuilder sb, Dictionary<string, int> map)
    {
        List<string> keys = new List<string>(map.Keys);
        keys.Sort(delegate(string a, string b)
        {
            int d = map[b].CompareTo(map[a]);
            if (d != 0) return d;
            return string.Compare(a, b, StringComparison.Ordinal);
        });
        for (int i = 0; i < keys.Count; i++)
            sb.AppendLine("| " + keys[i] + " | " + map[keys[i]] + " |");
    }

    // The table stores kind labels in the UI language; the docs are English.
    private static string KindName(string kind)
    {
        if (kind == "鼠标") return "Mouse";
        if (kind == "键盘") return "Keyboard";
        if (kind == "耳机") return "Headset";
        if (kind == "手柄") return "Controller";
        if (kind == "接收器") return "Receiver";
        if (kind == "麦克风") return "Microphone";
        if (kind == "拓展坞") return "Dock";
        return kind;
    }

    private static bool IsVerified(DeviceModel m)
    {
        for (int i = 0; i < Verified.Length; i++)
            if (Verified[i] == m.Key) return true;
        return false;
    }

    private static string Pids(ushort[] list)
    {
        if (list == null || list.Length == 0) return "—";
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < list.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(list[i].ToString("X4", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    private static string Escape(string value)
    {
        if (value == null) return "";
        return value.Replace("|", "\\|");
    }
}
