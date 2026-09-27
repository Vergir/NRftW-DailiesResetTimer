using System;
using Il2CppMoon.Forsaken;
using Il2CppMoon.TheLoop;
using Il2CppQuantum;

namespace DailiesResetTimer;

/// <summary>Time left until the next daily and weekly reset of one activity system (bounties or challenges).</summary>
internal readonly struct ResetTimes
{
    public readonly TimeSpan Daily;
    public readonly TimeSpan Weekly;
    public ResetTimes(TimeSpan daily, TimeSpan weekly) { Daily = daily; Weekly = weekly; }
}

/// <summary>
/// Reads the reset schedule straight from the simulation, the same state RepeatableActivitySystem.Update checks
/// (see docs/internal.md):
///   daily  - fires once the realm's real-time clock passes DailyResetHour:00 after LastDailyResetTime;
///   weekly - fires at the first day boundary at least DaysPerWeeklyReset days after LastWeeklyResetTime's day.
/// FPDateTime ticks are .NET ticks (100 ns); the game's day boundary is ticks / TimeSpan.TicksPerDay.
/// </summary>
internal static class ResetClock
{
    /// <summary>False outside a realm (main menu, loading) or when the activity system has no schedule.</summary>
    public static unsafe bool TryGet(ActivityType type, out ResetTimes times)
    {
        times = default;
        var viewFrame = ViewFrame.s_activeViewFrame;
        if (viewFrame == null) return false;
        var frame = viewFrame.GetQtn();
        if (frame == null) return false;

        ActivitySystemAttachments* system = ActivityDetailsPanel.GetSystemAttachments(frame, type);
        if (system == null) return false;
        RepeatableActivityEntryConfig cfg = system->RepeatabilityConfig;
        if (cfg.DaysPerWeeklyReset <= 0) return false;

        long now = TimeAPI.GetCurrentRealTime(frame).Ticks;
        long nextDaily = NextDaily(cfg.LastDailyResetTime.Ticks, cfg.DailyResetHour, now);
        long nextWeekly = NextWeekly(cfg.LastWeeklyResetTime.Ticks, cfg.DaysPerWeeklyReset, now);
        times = new ResetTimes(TimeSpan.FromTicks(nextDaily - now), TimeSpan.FromTicks(nextWeekly - now));
        return true;
    }

    /// <summary>Realm real time as a UTC DateTime, for the log (checks the tick basis).</summary>
    public static bool TryGetRealmNow(out DateTime now)
    {
        now = default;
        var frame = ViewFrame.s_activeViewFrame?.GetQtn();
        if (frame == null) return false;
        long ticks = TimeAPI.GetCurrentRealTime(frame).Ticks;
        if (ticks <= 0 || ticks >= DateTime.MaxValue.Ticks) return false;
        now = new DateTime(ticks, DateTimeKind.Utc);
        return true;
    }

    private static long DayStart(long ticks) => ticks / TimeSpan.TicksPerDay * TimeSpan.TicksPerDay;

    private static long NextDaily(long last, int resetHour, long now)
    {
        long hour = resetHour * TimeSpan.TicksPerHour;
        long next = DayStart(last) + hour;
        if (last - DayStart(last) >= hour) next += TimeSpan.TicksPerDay;
        // Overdue (new realm, or the sim has not run its 10 s check yet): the reset is about to happen,
        // so show the one after it.
        if (next <= now)
        {
            next = DayStart(now) + hour;
            if (next <= now) next += TimeSpan.TicksPerDay;
        }
        return next;
    }

    private static long NextWeekly(long last, int days, long now)
    {
        long next = DayStart(last) + days * TimeSpan.TicksPerDay;
        if (next <= now) next = DayStart(now) + days * TimeSpan.TicksPerDay;
        return next;
    }

    // Digit groups get a fixed advance so a right-aligned or centered countdown does not shift as digits change.
    // <mspace> is understood by both TextMeshPro (shop) and UI Toolkit (board, Quests menu).
    private const string DigitWidth = "0.6em";

    /// <summary>
    /// Default: the two largest units, "5d 08h", "13h 53m", "42m 10s". Full (the AlwaysShowFullTime setting, unless
    /// <paramref name="full"/> says otherwise): every unit from the largest down, "5d 08h 53m 10s". Rich text with
    /// monospaced digits unless plain.
    /// </summary>
    public static string Format(TimeSpan t, bool plain = false, bool? full = null)
    {
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
        string N(int v, string fmt) => plain ? v.ToString(fmt) : $"<mspace={DigitWidth}>{v.ToString(fmt)}</mspace>";
        if (full ?? Prefs.AlwaysShowFullTime.Value)
        {
            if (t.Days > 0) return $"{N(t.Days, "0")}d {N(t.Hours, "00")}h {N(t.Minutes, "00")}m {N(t.Seconds, "00")}s";
            if (t.Hours > 0) return $"{N(t.Hours, "0")}h {N(t.Minutes, "00")}m {N(t.Seconds, "00")}s";
            return $"{N(t.Minutes, "0")}m {N(t.Seconds, "00")}s";
        }
        if (t.Days > 0) return $"{N(t.Days, "0")}d {N(t.Hours, "00")}h";
        if (t.Hours > 0) return $"{N(t.Hours, "0")}h {N(t.Minutes, "00")}m";
        return $"{N(t.Minutes, "0")}m {N(t.Seconds, "00")}s";
    }
}
