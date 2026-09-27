using MelonLoader;

namespace DailiesResetTimer;

/// <summary>User settings, stored in UserData/MelonPreferences.cfg under [DailiesResetTimer].
/// The Show* switches and AlwaysShowFullTime are also checkboxes in Options > Gameplay.</summary>
internal static class Prefs
{
    public static MelonPreferences_Entry<bool> Enabled = null!;
    public static MelonPreferences_Entry<bool> ShowOnActivityBoard = null!;
    public static MelonPreferences_Entry<bool> ShowInQuestLog = null!;
    public static MelonPreferences_Entry<bool> ShowOnVendors = null!;
    public static MelonPreferences_Entry<bool> AlwaysShowFullTime = null!;
    public static MelonPreferences_Entry<bool> AddSettingsRows = null!;

    public static void Init()
    {
        var cat = MelonPreferences.CreateCategory("DailiesResetTimer", "Dailies Reset Timer");
        Enabled = cat.CreateEntry("Enabled", true, description: "Master switch.");
        ShowOnActivityBoard = cat.CreateEntry("ShowOnActivityBoard", true,
            description: "Bounty board and challenge NPC screens: live countdowns next to the hourglasses of the Daily and Weekly lists. Also a checkbox in Options > Gameplay.");
        ShowInQuestLog = cat.CreateEntry("ShowInQuestLog", true,
            description: "Player menu > Quests, Bounties and Challenges tabs: a line with both countdowns above the list. Also a checkbox in Options > Gameplay.");
        ShowOnVendors = cat.CreateEntry("ShowOnVendors", true,
            description: "Vendors with limited stock: countdown to the daily restock above their items. Also a checkbox in Options > Gameplay.");
        AlwaysShowFullTime = cat.CreateEntry("AlwaysShowFullTime", false,
            description: "Show every unit down to seconds (5d 08h 53m 10s) instead of the two largest (5d 08h). Also a checkbox in Options > Gameplay.");
        AddSettingsRows = cat.CreateEntry("AddSettingsRows", true,
            description: "Add this mod's checkboxes to Options > Gameplay.");
    }
}
