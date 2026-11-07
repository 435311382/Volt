using System;

namespace Volt
{
    /// <summary>
    /// Filter 的行为测试。不带任何测试框架，直接编译成控制台程序跑，
    /// 因为这台机器上没有 SDK、没有 NuGet，只有系统自带的 csc.exe。
    /// </summary>
    public static class FilterTests
    {
        private static int _failed;
        private static int _passed;

        private static void Check(string name, int actual, int expected)
        {
            if (actual == expected)
            {
                _passed++;
                Console.WriteLine("  ok   " + name + " -> " + actual);
            }
            else
            {
                _failed++;
                Console.WriteLine("  FAIL " + name + " -> got " + actual + ", want " + expected);
            }
        }

        public static int Main()
        {
            BatteryFilter f;

            Console.WriteLine("基本行为");

            f = new BatteryFilter(85);
            Check("初始值", f.Last, 85);

            f = new BatteryFilter(85);
            Check("掉一个点是自然放电", f.Apply(84, ChargeState.Discharging, 5000), 84);
            Check("再掉一个点", f.Apply(83, ChargeState.Discharging, 5000), 83);

            f = new BatteryFilter(-1);
            Check("没有初值时第一口就采信", f.Apply(77, ChargeState.Discharging, 5000), 77);

            Console.WriteLine();
            Console.WriteLine("放电中不许涨（微软 BAS 的只降不升）");

            f = new BatteryFilter(81);
            Check("回弹到 84 不采信", f.Apply(84, ChargeState.Discharging, 5000), 81);
            Check("回弹第 2 次还是不采信", f.Apply(84, ChargeState.Discharging, 5000), 81);
            Check("回弹第 9 次也不采信", f.Apply(84, ChargeState.Discharging, 5000), 81);

            f = new BatteryFilter(60);
            Check("大涨到 90 不采信", f.Apply(90, ChargeState.Discharging, 5000), 60);

            f = new BatteryFilter(60);
            Check("未知充电状态也不许涨", f.Apply(70, ChargeState.Unknown, 5000), 60);

            Console.WriteLine();
            Console.WriteLine("充电中随便变");

            f = new BatteryFilter(81);
            Check("充电上涨立即采信", f.Apply(84, ChargeState.Charging, 5000), 84);
            Check("充电继续涨", f.Apply(90, ChargeState.Charging, 5000), 90);
            Check("充满", f.Apply(100, ChargeState.Full, 5000), 100);
            Check("充满后又掉到 99", f.Apply(99, ChargeState.Charging, 5000), 99);

            Console.WriteLine();
            Console.WriteLine("大幅下降要持续够久才采信");

            f = new BatteryFilter(80);
            Check("刚掉到 70，只有 5 秒，压住", f.Apply(70, ChargeState.Discharging, 5000), 80);
            Check("10 秒且第 2 次，采信", f.Apply(70, ChargeState.Discharging, 5000), 70);

            f = new BatteryFilter(80);
            Check("掉到 70 第 1 次", f.Apply(70, ChargeState.Discharging, 5000), 80);
            Check("回弹到 79 就重数", f.Apply(79, ChargeState.Discharging, 5000), 79);
            Check("再掉到 70 第 1 次", f.Apply(70, ChargeState.Discharging, 5000), 79);
            Check("再掉到 70 第 2 次采信", f.Apply(70, ChargeState.Discharging, 5000), 70);

            Console.WriteLine();
            Console.WriteLine("用户报的场景：充到 96，拔线变 84，又回 96");

            f = new BatteryFilter(96);
            Check("拔线第 1 次 84 压住", f.Apply(84, ChargeState.Discharging, 5000), 96);
            Check("拔线第 2 次 84 采信（累计 10 秒）", f.Apply(84, ChargeState.Discharging, 5000), 84);
            Check("爬回 96 不采信", f.Apply(96, ChargeState.Discharging, 5000), 84);
            Check("再爬 96 还是不采信", f.Apply(96, ChargeState.Discharging, 5000), 84);
            Check("插回去充电 96 采信", f.Apply(96, ChargeState.Charging, 5000), 96);

            Console.WriteLine();
            Console.WriteLine("坏值");

            f = new BatteryFilter(81);
            Check("超范围 116", f.Apply(116, ChargeState.Discharging, 5000), 81);
            Check("超范围 127", f.Apply(127, ChargeState.Discharging, 5000), 81);
            Check("零值", f.Apply(0, ChargeState.Discharging, 5000), 81);
            Check("负值", f.Apply(-5, ChargeState.Discharging, 5000), 81);
            Check("坏值之后正常下降照样能写", f.Apply(80, ChargeState.Discharging, 5000), 80);

            Console.WriteLine();
            Console.WriteLine("长时间间隔后重新同步");

            f = new BatteryFilter(40);
            Check("隔 10 分钟涨到 90 采信", f.Apply(90, ChargeState.Discharging, 600000), 90);
            f = new BatteryFilter(90);
            Check("隔 10 分钟掉到 40 采信", f.Apply(40, ChargeState.Discharging, 600000), 40);
            f = new BatteryFilter(40);
            Check("隔 5 秒涨到 90 不采信", f.Apply(90, ChargeState.Discharging, 5000), 40);

            Console.WriteLine();
            Console.WriteLine("重新同步");

            f = new BatteryFilter(40);
            f.Resync(95);
            Check("Resync 直接改值", f.Last, 95);
            f.Resync(0);
            Check("Resync 坏值被忽略", f.Last, 95);
            f.Resync(200);
            Check("Resync 超范围被忽略", f.Last, 95);

            Console.WriteLine();
            Console.WriteLine("固件在高值和低值之间来回抖");

            f = new BatteryFilter(96);
            Check("96 -> 84 压住", f.Apply(84, ChargeState.Discharging, 5000), 96);
            Check("抖回 96", f.Apply(96, ChargeState.Discharging, 5000), 96);
            Check("再掉 84，累计不清零，采信", f.Apply(84, ChargeState.Discharging, 5000), 84);

            Console.WriteLine();
            Console.WriteLine("passed " + _passed + ", failed " + _failed);
            return _failed == 0 ? 0 : 1;
        }
    }
}
