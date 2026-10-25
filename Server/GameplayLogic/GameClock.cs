using System;

namespace SphServer.Server.GameplayLogic;

/// <summary>
///     The world clock the client shows (HUD time and the sky). Retail sends it as the _main ClientSky counter
///     every 15 s: 0.8 units per real second (1 unit = 15 game seconds, 1 game minute = 5 real seconds), a game
///     day is 5760 units, and the time of day is (counter - 3587) mod 5760; both constants were fitted to two
///     clock readings against the captured counters (08:00 and 08:36 within 30 s). The counter here follows real
///     time, shifted by an offset that /time changes.
/// </summary>
public static class GameClock
{
    private const double UnitsPerSecond = 0.8;
    private const int UnitsPerDay = 5760;
    private const int UnitsPerHour = 240;
    private const int UnitsPerMinute = 4;
    private const int MidnightAlignment = 3587;

    private static long offsetUnits;

    public static uint Counter =>
        (uint)((long)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0 * UnitsPerSecond) + offsetUnits);

    private static int TimeOfDayUnits => (int)(((long)Counter - MidnightAlignment) % UnitsPerDay + UnitsPerDay) % UnitsPerDay;

    public static string TimeOfDay
    {
        get
        {
            var units = TimeOfDayUnits;
            return $"{units / UnitsPerHour:00}:{units % UnitsPerHour / UnitsPerMinute:00}";
        }
    }

    public static void SetTimeOfDay(int hour, int minute)
    {
        var target = hour * UnitsPerHour + minute * UnitsPerMinute;
        offsetUnits += ((target - TimeOfDayUnits) % UnitsPerDay + UnitsPerDay) % UnitsPerDay;
    }
}
