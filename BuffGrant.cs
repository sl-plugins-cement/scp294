using System;
using System.Collections.Generic;
using System.Globalization;

namespace Qlz;

internal enum Drink { QiaoLeZi, SixtySeven, Ahead, Vodka, CompoundV, Meteor, Jiahao, Scp207 }
internal enum BuffBranch { Primary, Secondary, Random }

internal sealed class BuffGrant
{
    internal const string Catalog = "普通SCP-207(scp207，给予原生瓶子)、巧乐兹(qlz)、67、遥遥领先(ahead)、伏特加(vodka)、5号化合物(v)、美味流星(meteor)、嘉豪(jiahao)";
    public Drink Drink { get; }
    public float? Duration { get; }
    public BuffBranch Branch { get; }

    private BuffGrant(Drink drink, float? duration, BuffBranch branch)
    {
        Drink = drink;
        Duration = duration;
        Branch = branch;
    }

    internal static bool TryParse(string name, IEnumerable<string> options, out BuffGrant? grant, out string error)
    {
        grant = null;
        error = string.Empty;
        Drink drink;
        switch (name.ToLowerInvariant().TrimEnd('?', '？'))
        {
            case "scp207": case "207": case "普通207": case "正常207": case "普通scp-207":
            case "普通咖啡": case "咖啡": case "coffee": drink = Drink.Scp207; break;
            case "巧乐兹": case "qlz": case "qiaolezi": drink = Drink.QiaoLeZi; break;
            case "67": case "sixtyseven": drink = Drink.SixtySeven; break;
            case "遥遥领先": case "ahead": drink = Drink.Ahead; break;
            case "伏特加": case "vodka": drink = Drink.Vodka; break;
            case "5号化合物": case "v": case "compoundv": drink = Drink.CompoundV; break;
            case "美味流星": case "meteor": drink = Drink.Meteor; break;
            case "嘉豪": case "嘉豪の圣遗物": case "jiahao": drink = Drink.Jiahao; break;
            default: error = "未知 Buff。可用：" + Catalog; return false;
        }
        float? duration = null;
        BuffBranch branch = BuffBranch.Primary;
        bool hasBranch = false;
        foreach (string option in options)
        {
            if (float.TryParse(option, NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds))
            {
                if (duration.HasValue || float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0.1f || seconds > 3600f)
                {
                    error = "持续秒数只能指定一次，范围 0.1～3600。";
                    return false;
                }
                duration = seconds;
                continue;
            }
            if (hasBranch) { error = "效果分支只能指定一次。"; return false; }
            switch (option.ToLowerInvariant())
            {
                case "primary": branch = BuffBranch.Primary; break;
                case "speed": case "加速":
                    if (drink != Drink.QiaoLeZi) { error = "speed 仅用于巧乐兹。"; return false; }
                    branch = BuffBranch.Primary; break;
                case "success": case "成功":
                    if (drink != Drink.Jiahao) { error = "success 仅用于嘉豪。"; return false; }
                    branch = BuffBranch.Primary; break;
                case "cardiac": case "心脏骤停":
                    if (drink != Drink.QiaoLeZi) { error = "cardiac 仅用于巧乐兹。"; return false; }
                    branch = BuffBranch.Secondary; break;
                case "fail": case "失败":
                    if (drink != Drink.Jiahao) { error = "fail 仅用于嘉豪，会直接死亡。"; return false; }
                    branch = BuffBranch.Secondary; break;
                case "random": case "随机":
                    if (drink != Drink.QiaoLeZi && drink != Drink.Jiahao) { error = "random 仅用于巧乐兹或嘉豪。"; return false; }
                    branch = BuffBranch.Random; break;
                default: error = "未知参数：" + option + "。可填写秒数或分支 speed/cardiac/success/fail/random。"; return false;
            }
            hasBranch = true;
        }
        if (drink == Drink.Scp207 && (duration.HasValue || hasBranch))
        { error = "普通SCP-207给予原生瓶子，不接受自定义秒数或分支。"; return false; }
        grant = new BuffGrant(drink, duration, branch);
        return true;
    }
}
