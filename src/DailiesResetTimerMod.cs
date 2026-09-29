using System;
using DailiesResetTimer;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(DailiesResetTimerMod), "Dailies Reset Timer", "1.0.0", "vergir")]
[assembly: MelonGame("Moon Studios", "NoRestForTheWicked")]
// MelonLoader would otherwise apply every [HarmonyPatch] itself, twice with our PatchAll and even when Enabled = false.
[assembly: HarmonyDontPatchAll]

namespace DailiesResetTimer;

/// <summary>Entry point. See docs/internal.md.</summary>
public class DailiesResetTimerMod : MelonMod
{
    public static DailiesResetTimerMod Instance { get; private set; } = null!;
    public static MelonLogger.Instance Log => Instance.LoggerInstance;

    private const float UpdateInterval = 0.2f;
    private float _nextUpdate;
    private bool _loggedClock;
    private string? _lastError;

    // One per screen: shown while its switch is on, taken off the screen once when it is turned off.
    private readonly Feature _board = new Feature("board", () => Prefs.ShowOnActivityBoard.Value, VendorScreenTimers.Update, VendorScreenTimers.Restore);
    private readonly Feature _questLog = new Feature("quest log", () => Prefs.ShowInQuestLog.Value, QuestLogTimers.Update, QuestLogTimers.Remove);
    private readonly Feature _shop = new Feature("shop", () => Prefs.ShowOnVendors.Value, ShopTimer.Update, ShopTimer.Remove);

    public override void OnInitializeMelon()
    {
        Instance = this;
        Prefs.Init();
        if (!Prefs.Enabled.Value)
        {
            LoggerInstance.Msg("Disabled via preferences.");
            return;
        }
        HarmonyInstance.PatchAll(typeof(DailiesResetTimerMod).Assembly);

        // After a hot reload the settings screens already exist; the Initialize postfix will not run for them.
        if (Prefs.AddSettingsRows.Value)
        {
            try { SettingsRows.AddToLiveScreens(); }
            catch (Exception e) { LoggerInstance.Warning("Adding rows to live settings screens: " + e.Message); }
        }
    }

    /// <summary>Unload / hot reload: take our rows, text and labels back off the game's screens.</summary>
    public override void OnDeinitializeMelon()
    {
        try { SettingsRows.RemoveAll(); }
        catch (Exception e) { LoggerInstance.Warning("SettingsRows.RemoveAll: " + e.Message); }
        _board.Cleanup(LoggerInstance);
        _questLog.Cleanup(LoggerInstance);
        _shop.Cleanup(LoggerInstance);
    }

    // LateUpdate: after the screens' own Update wrote their labels, before the UI renders.
    public override void OnLateUpdate()
    {
        if (!Prefs.Enabled.Value || Time.unscaledTime < _nextUpdate) return;
        _nextUpdate = Time.unscaledTime + UpdateInterval;
        try
        {
            if (!_loggedClock) LogClockOnce();
            _board.Tick(LoggerInstance);
            _questLog.Tick(LoggerInstance);
            _shop.Tick(LoggerInstance);
            _lastError = null;
        }
        catch (Exception e)
        {
            // Log a failure once, not five times a second.
            if (e.Message != _lastError) LoggerInstance.Warning("Update: " + e);
            _lastError = e.Message;
        }
    }

    /// <summary>Once per session in a realm: realm clock vs PC clock and the current countdowns, to check the tick basis.</summary>
    private void LogClockOnce()
    {
        if (!ResetClock.TryGetRealmNow(out var realmNow)) return;
        _loggedClock = true;
        string Times(Il2CppQuantum.ActivityType t) => ResetClock.TryGet(t, out var r)
            ? $"daily in {ResetClock.Format(r.Daily, plain: true, full: true)}, weekly in {ResetClock.Format(r.Weekly, plain: true, full: true)}" : "n/a";
        LoggerInstance.Msg($"Realm clock {realmNow:yyyy-MM-dd HH:mm:ss} (PC UTC {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}). " +
                           $"Bounties: {Times(Il2CppQuantum.ActivityType.Bounty)}. Challenges: {Times(Il2CppQuantum.ActivityType.Challenge)}. " +
                           $"Vendor restock: {Times(Il2CppQuantum.ActivityType.VendorInventoryRestock)}.");
    }

    private sealed class Feature
    {
        private readonly string _name;
        private readonly Func<bool> _on;
        private readonly Action _update, _cleanup;
        private bool _wasOn;

        public Feature(string name, Func<bool> on, Action update, Action cleanup)
        {
            _name = name; _on = on; _update = update; _cleanup = cleanup;
        }

        public void Tick(MelonLogger.Instance log)
        {
            bool on = _on();
            if (on) _update();
            else if (_wasOn) Cleanup(log);
            _wasOn = on;
        }

        public void Cleanup(MelonLogger.Instance log)
        {
            try { _cleanup(); }
            catch (Exception e) { log.Warning($"Cleaning up the {_name} timers: {e.Message}"); }
        }
    }
}
