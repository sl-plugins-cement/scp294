using System;

namespace Qlz;

internal static class DrinkRolls
{
    // Integer tickets give exact odds, including the 0.2% legendary roll.
    internal const int Tickets = 1000;
    private static readonly (Drink Drink, int Tickets)[] Pool =
    {
        (Drink.Coffee, 800),
        (Drink.Ahead, 100),
        (Drink.QiaoLeZi, 20),
        (Drink.SixtySeven, 30),
        (Drink.Vodka, 30),
        (Drink.CompoundV, 10),
        (Drink.Meteor, 8),
        (Drink.Jiahao, 2),
    };

    internal static Drink Pick(int ticket)
    {
        if (ticket < 0 || ticket >= Tickets) throw new ArgumentOutOfRangeException(nameof(ticket));
        foreach (var entry in Pool)
        {
            if (ticket < entry.Tickets) return entry.Drink;
            ticket -= entry.Tickets;
        }
        throw new InvalidOperationException("Drink weights must cover the ticket range.");
    }
}
