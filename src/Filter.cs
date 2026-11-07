using System;

namespace Volt
{
    /// <summary>
    /// 电量读数过滤器。
    ///
    /// 主规则来自微软对电池服务（BAS）的官方要求：电量必须当作「只降不升」的属性处理，
    /// 只有两种情况允许上升——设备接在充电器上，或者电池被取下重装。
    ///
    ///   原文：devices that implement BAS shall treat the measured Battery Level value
    ///   (0-100%) as a decrease-only attribute, where it is only allowed to increase
    ///   under one of two conditions: 1) The device is connected to a charger, or
    ///   2) The device's battery is removed and reinstalled
    ///
    ///   来源：https://learn.microsoft.com/en-us/windows-hardware/design/accessory-guidelines/bluetooth-accessory-guidelines/bluetooth-accessory-guidelines-battery-profiles-services
    ///
    /// 为什么必须这样：锂电池带负载时电压被拉低，负载一撤电压回升，按电压估出来的
    /// 电量就往上跳几个点。用户看到的就是「拔线变 80 多，再充一下又 90 多，来回跳」。
    /// 微软在同一篇里举的例子和这个场景一模一样——鼠标掉到 5% 后进入低功耗状态，
    /// 负载变小、电压升高，测出来变成 10%，但用户没做任何充电动作，
    /// 所以上报值必须仍然是 5%。
    ///
    /// 在这条主规则之外补了三处，都是为了不把程序自己搞坏：
    ///
    /// 一、大幅下降要「持续」才算数，不是读到一次就认。
    ///     固件偶尔吐出一个明显偏低的坏帧，直接采信就会把显示锁死在错误值上
    ///     （只降不升，就再也回不来了）。而且刚拔掉充电线的那一瞬间，
    ///     负载突然加上去，电压会系统性往下掉一截，这时候的读数是失真的，
    ///     等它稳定一会儿再看。判断依据是「持续低于阈值」，不是「一直读到同一个数」——
    ///     读数在 84 和 85 之间抖的时候，两个都在阈值以下，应该继续累计，不能重新计时。
    ///
    /// 二、超范围值直接丢弃。固件报过 116、123 这种数，那是它自己的测量问题。
    ///
    /// 三、长时间间隔后允许重新同步。电脑睡眠、休眠、程序刚启动这些情况下，
    ///     期间发生的事我们一无所知，继续坚持只降不升就会显示一个陈旧的错值。
    ///
    /// 纯状态机，不碰任何系统接口，可以单独测。
    /// </summary>
    public sealed class BatteryFilter
    {
        /// <summary>自然放电每次最多掉几个点；掉得更多就不当自然放电看。</summary>
        public const int MaxNaturalDrop = 1;

        /// <summary>大幅下降要持续这么久才采信，单位毫秒。</summary>
        public const long DropHoldMs = 10000;

        /// <summary>大幅下降要连续读到这么多次才采信。</summary>
        public const int DropConfirmations = 2;

        /// <summary>两次读数间隔超过这么久，就放弃「只降不升」，直接采信新值。</summary>
        public const long ResyncGapMs = 300000;

        /// <summary>调用方没给间隔时按这个算，等于「隔了一个可见轮询周期」。</summary>
        public const long DefaultElapsedMs = 5000;

        private int _last = -1;
        private long _belowMs;
        private int _belowCount;

        public BatteryFilter(int initial)
        {
            if (initial > 0 && initial <= 100) _last = initial;
        }

        public int Last { get { return _last; } }
        public bool HasValue { get { return _last > 0; } }

        /// <summary>
        /// 重新同步：直接把当前读数当新起点。
        ///
        /// 用在「设备重新出现」这种场合——设备消失又回来，
        /// 效果等同于微软规则里的第二个允许上升条件「电池被取下重装」：
        /// 中间那段时间我们什么都没看见，继续拿旧值压着，
        /// 显示的就只是一个再也涨不回去的死数字。
        /// </summary>
        public void Resync(int raw)
        {
            if (raw > 0 && raw <= 100) Accept(raw);
        }

        /// <summary>喂一个原始读数，返回应该显示的值。</summary>
        public int Apply(int raw, ChargeState charge)
        {
            return Apply(raw, charge, DefaultElapsedMs);
        }

        /// <summary>
        /// elapsedMs 是距离上一次喂读数过了多久；不知道就传 0。
        /// </summary>
        public int Apply(int raw, ChargeState charge, long elapsedMs)
        {
            if (raw <= 0 || raw > 100) return _last;

            if (_last < 0)
            {
                Accept(raw);
                return _last;
            }

            // 接在充电器上：充电过程中上升是真实的，下降也可能是真的（拔了又插），
            // 两种情况都直接采信。
            if (charge == ChargeState.Charging || charge == ChargeState.Full)
            {
                Accept(raw);
                return _last;
            }

            // 隔了太久，期间发生什么都不知道，重新同步
            if (elapsedMs >= ResyncGapMs)
            {
                Accept(raw);
                return _last;
            }

            if (raw == _last)
            {
                // 和当前显示值一样，什么都不用做。
                // 这里刻意不调用 Accept：Accept 会清掉下面的下跌累计，
                // 而固件在 96、84 之间来回抖的时候，中间夹一个 96
                // 不应该让已经攒到的下跌次数归零。
                return _last;
            }

            if (raw > _last)
            {
                // 上升，而设备没在充电：按微软的规则不采信。
                // 这不是丢数据，是拒绝把电压回弹当成充电。
                //
                // 注意这里刻意不清空下面的下跌计数：固件可能在高值和低值之间
                // 来回抖（96、84、96、84……）。如果一看到回升就把计数清零，
                // 这种抖动会让下跌永远攒不够、显示永远卡在高值上。
                // 回升本身不加分，但也不该把已经攒到的分抹掉。
                return _last;
            }

            // 掉一个点以内是自然放电，直接采信
            if (_last - raw <= MaxNaturalDrop)
            {
                Accept(raw);
                return _last;
            }

            // 掉得更多：可能是真的掉电，也可能是拔线瞬间的负载压降。
            // 只要读数持续待在阈值以下，就一路累计；什么时候回到阈值以上，
            // 什么时候重新计时。中途在 84/85 之间抖动不影响累计。
            _belowMs += elapsedMs;
            _belowCount++;

            if (_belowMs >= DropHoldMs && _belowCount >= DropConfirmations)
            {
                Accept(raw);
                return _last;
            }

            return _last;
        }

        private void Accept(int raw)
        {
            _last = raw;
            _belowMs = 0;
            _belowCount = 0;
        }
    }
}
