using System;

namespace Qlz;

internal sealed class DrinkRolls
{
    // One ticket is 0.01%; disabled drinks have no tickets and cannot be selected.
    internal const int Tickets = 10000;
    private readonly (Drink Drink, int Tickets)[] pool;
    private DrinkRolls((Drink Drink, int Tickets)[] pool) => this.pool = pool;

    internal static bool TryCreate(Config config, out DrinkRolls? rolls, out string error)
    {
        var chances = new (Drink Drink, double Percent)[]
        {
            (Drink.Scp207, config.Scp207ChancePercent),
            (Drink.Ahead, config.AheadChancePercent),
            (Drink.QiaoLeZi, config.QiaoLeZiChancePercent),
            (Drink.SixtySeven, config.SixtySevenChancePercent),
            (Drink.Vodka, config.VodkaChancePercent),
            (Drink.CompoundV, config.CompoundVChancePercent),
            (Drink.Meteor, config.MeteorChancePercent),
            (Drink.Jiahao, config.JiahaoChancePercent),
        };
        rolls = null;
        error = string.Empty;
        var entries = new (Drink Drink, int Tickets)[chances.Length];
        int total = 0;
        for (int i = 0; i < chances.Length; i++)
        {
            double percent = chances[i].Percent;
            if (double.IsNaN(percent) || double.IsInfinity(percent) || percent < 0 || percent > 100)
            { error = chances[i].Drink + " chance must be finite and between 0 and 100%."; return false; }
            double tickets = percent * 100;
            if (Math.Abs(tickets - Math.Round(tickets)) > 0.000001)
            { error = chances[i].Drink + " chance supports at most two decimal places."; return false; }
            int count = (int)Math.Round(tickets);
            entries[i] = (chances[i].Drink, count);
            total += count;
        }
        if (total != Tickets)
        { error = "Drink chances must total exactly 100%."; return false; }
        rolls = new DrinkRolls(entries);
        return true;
    }

    internal Drink Pick(int ticket)
    {
        if (ticket < 0 || ticket >= Tickets) throw new ArgumentOutOfRangeException(nameof(ticket));
        foreach (var entry in pool)
        {
            if (ticket < entry.Tickets) return entry.Drink;
            ticket -= entry.Tickets;
        }
        throw new InvalidOperationException("Drink weights must cover the ticket range.");
    }
}
