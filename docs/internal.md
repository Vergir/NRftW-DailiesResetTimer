# Dailies Reset Timer: how it works

Research notes for maintainers. Addresses are RVAs in Steam build 29466 (`reference/GameAssembly.dll`, `dump.cs`).
They move with every game update, but the class, method and field names usually do not.

## Where the schedule lives

Bounties and challenges are "repeatable activities". Each activity system (bounties, challenges, vendor restock...)
owns one ECS component, `Quantum.ActivitySystemAttachments`, with a `RepeatableActivityEntryConfig RepeatabilityConfig`
at offset 0x48:

| Field | Offset | Meaning |
|---|---|---|
| `DailyActivityCount` | 0x0 | dailies served per reset |
| `DailyResetHour` | 0x4 | hour of the daily reset |
| `DaysPerWeeklyReset` | 0x8 | days between weekly resets |
| `WeeklyActivityCount` | 0xC | weeklies served per reset |
| `LastDailyResetTime` | 0x18 | `FPDateTime` of the last daily reset |
| `LastScheduleUpdateTime` | 0x20 | last time the system checked (every 10 s) |
| `LastWeeklyResetTime` | 0x28 | `FPDateTime` of the last weekly reset |

`ChallengeActivitySystem.InitializeSystemState` (0x5D20B60) fills it from `SimulationConfig.ChallengeActivityConfig`
(`DailyResetTime` -> `DailyResetHour`, `ChallengesPerDay/Week` -> counts). `DaysPerWeeklyReset` is hard-coded to **7**,
and both `Last*ResetTime` fields start at a far-past sentinel (`0xFFFFF82457DE8000`), so a new world resets at once.
Bounties use `SimulationConfig.BountiesConfig` the same way. In `quantumDatabase.bin`, both `DailyResetTime` values are **5**.

The state is persisted with the world (`RepeatableActivitySystemPersistentState`), so the weekly schedule belongs to the
world, not the player.

## The reset rule

`RepeatableActivitySystem.Update` (0x5E59B70), for every `ActivitySystemAttachments` with a pool and a non-zero count,
at most every 10 s (`FPDateTime.DidSecondsPassBetween(LastScheduleUpdateTime, now, 10)`):

```
now = Globals.PersistentClock.CurrentRealTime          // frame globals + 0x728 + 0x8
if (now.Ticks / TicksPerDay - LastWeeklyResetTime.Ticks / TicksPerDay >= DaysPerWeeklyReset)
    ServeNewWeeklies()                                  // sets LastWeeklyResetTime = now
if (FPDateTime.DidTimeOfDayOccurBetween(LastDailyResetTime, now, DailyResetHour))
    ServeNewDailies()                                   // sets LastDailyResetTime = now
LastScheduleUpdateTime = now
```

`DidTimeOfDayOccurBetween(last, now, H)` (0x6009060) is true when more than one day boundary lies between them, or
when an `H:00` lies in `(last, now]` by whole hours. `UpdateCheckDailyReset` (0x5E59FD0) is the same check without the
10 s gate.

The resulting schedule:

* **Daily**: at `DailyResetHour:00` on the realm clock, the first one after the last daily reset.
* **Weekly**: at `00:00` on the realm clock, `DaysPerWeeklyReset` days after the day of the last weekly reset. It is not
  tied to a weekday, and it drifts when nobody is in the world when it falls due: the next weekly counts from the
  day it actually happened.

`FPDateTime.Ticks` are .NET ticks (100 ns, `TicksPerDay = 0xC92A69C000`). `CurrentRealTime` =
`RuntimeConfig.User.SessionStartRealTime` + the game's elapsed-time counters (`PersistentClockSystem.UpdateRealTime`,
0x60099E0). `GetCurrentLocalRealTime` adds `RealmLocalTimeZoneOffset` to it, so the plain real time is UTC.
The mod logs the realm clock next to the PC's UTC clock once per session; in-game they agree to within the log
latency (verified 2026-09-27), so the daily reset is 05:00 UTC.

## What the game shows

`ActivityVendorScreen.UpdateDailyResetTime` (0x8E41160) sets the first `daily-reset-time-label` from
`DateTime.Now` (the **local** clock) and `SimulationConfig.ChallengeActivityConfig.DailyResetTime`: "today at H:00, or
tomorrow if past". It runs when the screen is shown and does not tick. The second label, in the Weekly list header,
is never written and stays `--:--`. Neither is visible anyway: `ActivityVendorWindow.uss` (static_scenes path_id 268)
styles `.daily-time-container` as `position: absolute; width: 100px; height: 40px; top: -12%; right: -18%;
margin-right: 20px; font-size: 24px` and then `display: none`. The hourglass is `24 x 24px`, and the label has
an inline `flex: 1` from the UXML. The rest of the daily and weekly styles align that label's text differently.

`ActivityVendorScreen.CalculateLastWeeklyResetTime(resetHour, DayOfWeek)` only drives the 7 "Day N" diamonds of the
weekly progress bar.

## What the mod does

* `ResetClock`: `ViewFrame.s_activeViewFrame.GetQtn()` -> `ActivityDetailsPanel.GetSystemAttachments(frame, type)`
  (a private static helper; the interop exposes it) -> the config above, and `TimeAPI.GetCurrentRealTime(frame)`
  for "now". It applies the rule above. When a reset is overdue (the sim has not run its 10 s check yet, or a new
  world), it shows the reset after that one.
* `VendorScreenTimers`: bounties and challenges are two separate `ActivityVendorScreenPanel`s. The board's tabs switch
  between them, and the static `Instance` is only one of them, so every panel's `Screen` is checked while it
  `IsVisible`. Both `daily-reset-time-label`s get a time for `m_activityType`: the one under `.weeklycount` gets the
  weekly time. Each container is shown with inline styles: `display: flex`, absolute `left/right: 0`,
  `bottom: 100%`, and `top/width: auto`. This centers it just above its header ("HITS 0/4"), because the header row
  has no room: the list's "Daily" / "Weekly" title sits right under it. The label gets `flex-grow: 0`,
  `flex-basis: auto` and left-aligned text. Its inline `flex: 1` means grow 1 and basis 0. With basis 0 and no grow, the
  label is laid out 3 units wide and the text spills to one side, which puts the group off-center. The group's center
  then matches the title's to within a unit (824.6 vs 823.7). The `drt-shown` class marks containers we changed.
* `QuestLogTimers`: a two-line `Label` named `DRT_QuestLogResetTimer` inserted above the lists
  (`ActivityScreenUpdate.m_controls.Bounties.ListView`'s parent), 18px, `top: -20px`. The lists are absolutely
  positioned 46 units below that container's top and do not make room for it; the container starts 20 units below
  the tab divider. It shows on the Bounty and Challenge categories (`m_state.ActiveCategory`).
  `ActivityScreenPanel` stays active while the menu is closed.
* `ShopTimer`: see "Vendor restock".
* `ResetClock.Format`: the two largest units ("5d 08h", "13h 53m", "42m 10s"), or every unit down to seconds with the
  `AlwaysShowFullTime` setting ("5d 08h 53m 10s"), with each digit group in
  `<mspace=0.6em>` so right-aligned or centered text does not shift as digits change. TextMeshPro and UI Toolkit
  both render the tag.
* Everything runs from `OnLateUpdate`, 5 times a second. `OnLateUpdate` runs after the screens' own `Update` and before
  the UI renders. The only Harmony patch adds the settings rows. It is applied by the mod's own `PatchAll`; `[assembly: HarmonyDontPatchAll]` stops MelonLoader from applying it a second time (and when `Enabled = false`). Turning a switch off removes that screen's text once.

## Vendor restock

`VendorInventoryRestockActivitySystem.InitializeSystemState` (0x5E6C5xx) fills the same `RepeatableActivityEntryConfig`
from `SimulationConfig.VendorConfig.RestockableVendorDailyResetTime` (5 in the qdb), with `DailyActivityCount` and
`WeeklyActivityCount` = `int.MaxValue`. So every daily reset serves every vendor's restock activity, and
`VendorInventoryRestockDirector.OnActivityActivated` -> `TryRestockVendor` (0x5E6CCA0) clears and refills that vendor's
inventory entity. The restock time is `ResetClock.TryGet(ActivityType.VendorInventoryRestock).Daily`.

The shop is `LimitedStockVendorScreen` (uGUI, `playerMenu/foreground/limitedStockVendorScreen`). The game's own
`FindRestockActivity(Frame)` tells whether the vendor on screen restocks; the timer is hidden when it returns null.
`VendorScreenV2` is the recipe/crafting vendor and has no restock. The shop has no restock text, so `ShopTimer` adds a
`TextMeshProUGUI` named `DRT_RestockTimer` under `VendorInventory` (an `InventoryItemsElement` named
`vendorInventoryElementShared`), in its `vendorTabShared` (430 x 35, top of the panel). That tab's `title/text` is the
"Vendor" heading: 28pt, with its bottom edge 1 unit above the tab's top. The label copies its font and color. It is
anchored top-right with pivot (1,0) at y = 12 (clear of the first row of slots), 22pt, right-aligned, with
`LayoutElement.ignoreLayout`. The panel is shared with other vendor screens, so the label is hidden whenever the
limited-stock screen is not active.

IL2CPP pitfall: the interop's `Transform.SetParent(Transform, bool)` throws "Method unstripping failed". Unity 6 only
has the native 3-argument `SetParent(parent, worldPositionStays, disableTransformMessage)`, so use that.

## Settings rows

Checkboxes at the end of Options > Gameplay, after the same blank divider Display & UI Tweaks uses
(`AddDividerItem`). The game's toggles (`AddBoundToggleItem`) need a `PlayerSetting<bool>`, whose getter is a
`ref T` delegate that a mod cannot supply. `SettingsScreenControls.AddKeyboardAndMouseSchemeToggleItem(category,
name, initial, Action<bool>, style, desc, invokeOnStart)` builds the same `ToggleSettingsItemGUI` from a plain callback.
The style only matters while the row is hovered:

* it picks the glyph-token set for the description text (irrelevant, since ours has no tokens);
* for styles 0, 2 and 3, `SettingsScreenControls.Update` lets the "preview scheme" button (input 0xF3) open the
  keyboard layout preview.

So the rows use `Alternate1` (1). Rows are added in a postfix on `GameplaySettingsTab.Initialize`, which is called from
`SettingsScreen.Initialize` once per scene load, and on init for screens that already exist (hot reload). They are
named `DRT_*` and removed on unload the same way as in Display & UI Tweaks.

## UI assets

`static_scenes_all_*.bundle`: `ActivityVendorWindow` (path_id 259, NPC board) and `ActivityWindow` (258, Quests menu)
are the UXML `VisualTreeAsset`s. The strings can be read with a length-prefixed string scan of the raw MonoBehaviour
data (UnityPy `get_raw_data()`).

`ActivityVendorWindow.uss` is the `StyleSheet` at path_id 268. Read it with UnityPy `read_typetree()`: `m_ComplexSelectors` ->
`ruleIndex` -> `m_Rules[].m_Properties[]`, with values indexed into `floats` / `dimensions` / `colors` / `strings`.
Dimension units are 0 = unitless, 1 = px, 2 = %.

The live layouts of all three screens were checked with a temporary tree dump (UI Toolkit: name, classes,
`resolvedStyle`, `worldBound`; uGUI: RectTransform, components, TMP text). In UI Toolkit, iterate `VisualElement.classList`,
not `GetClasses()`: that enumerator throws "Collection was modified".

## Hot reload

`OnDeinitializeMelon` removes the settings rows, the Quests label and the shop label (both found by name), and hides
the board containers again (found by the `drt-shown` class), putting `--:--` back on the weekly label.
