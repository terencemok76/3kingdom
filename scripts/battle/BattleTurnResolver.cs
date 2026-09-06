using System;

namespace ThreeKingdom.Battle;

internal static class BattleTurnResolver
{
    internal static (int Year, int Month, int Day) AdvanceDate(int year, int month, int day)
    {
        day++;
        if (day <= DateTime.DaysInMonth(year, month))
        {
            return (year, month, day);
        }

        day = 1;
        month++;
        if (month <= 12)
        {
            return (year, month, day);
        }

        return (year + 1, 1, day);
    }
}
