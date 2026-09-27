using Il2CppMoon.Forsaken;
using Il2CppQuantum;
using UnityEngine;
using UnityEngine.UIElements;

namespace DailiesResetTimer;

/// <summary>
/// Player menu > Quests (ActivityWindow.uxml, tabs QUESTS / BOUNTIES / CHALLENGES). Adds a label above the activity list
/// with both countdowns (two lines: the list column is only ~430 units wide), shown on the Bounties and Challenges tabs.
/// </summary>
internal static class QuestLogTimers
{
    // Found by name rather than remembered, so a hot-reloaded build finds (and replaces) the old build's label.
    private const string LabelName = "DRT_QuestLogResetTimer";
    private const float RescanInterval = 2f;

    private static ActivityScreenPanel? _panel;
    private static float _nextRescan;

    public static void Update()
    {
        var controls = FindPanel()?.Screen?.m_controls;
        var list = controls?.Bounties.ListView;
        var container = list?.hierarchy.parent;
        if (container == null) return;

        var label = UiTree.Find<Label>(container, LabelName) ?? Create(container);
        ActivityType? type = controls!.Root != null ? CategoryType(_panel!.Screen.m_state.ActiveCategory) : null;
        if (type == null || !ResetClock.TryGet(type.Value, out var times))
        {
            label.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
            return;
        }
        label.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
        UiTree.SetText(label, $"Daily reset in {ResetClock.Format(times.Daily)}\nWeekly reset in {ResetClock.Format(times.Weekly)}");
    }

    /// <summary>Unload / hot reload: remove the label.</summary>
    public static void Remove()
    {
        foreach (var p in Resources.FindObjectsOfTypeAll<ActivityScreenPanel>())
        {
            var container = p?.Screen?.m_controls?.Bounties.ListView?.hierarchy.parent;
            if (container == null) continue;
            foreach (var label in UiTree.FindAll<Label>(container, LabelName)) label.RemoveFromHierarchy();
        }
        _panel = null;
    }

    private static ActivityScreenPanel? FindPanel()
    {
        if (_panel != null) return _panel;
        if (Time.unscaledTime < _nextRescan) return null;
        _nextRescan = Time.unscaledTime + RescanInterval;
        foreach (var p in Resources.FindObjectsOfTypeAll<ActivityScreenPanel>())
            if (p != null && p.gameObject.scene.IsValid()) { _panel = p; break; }
        return _panel;
    }

    private static ActivityType? CategoryType(ActivityScreenUpdate.Category category) => category switch
    {
        ActivityScreenUpdate.Category.Bounty => ActivityType.Bounty,
        ActivityScreenUpdate.Category.Challenge => ActivityType.Challenge,
        _ => null,
    };

    private static Label Create(VisualElement container)
    {
        var label = new Label { name = LabelName, pickingMode = PickingMode.Ignore };
        label.AddToClassList("friz");
        var s = label.style;
        s.fontSize = new StyleLength(18f);
        s.color = new StyleColor(new Color(0.85f, 0.8f, 0.7f, 1f));
        s.unityTextAlign = new StyleEnum<TextAnchor>(TextAnchor.UpperLeft);
        // The lists are absolutely positioned 46 units below the container's top and do not make room for us; the
        // container starts 20 units below the tab divider. Shift up into that gap so both lines end above the list.
        s.top = new StyleLength(-20f);
        s.flexShrink = new StyleFloat(0f);
        container.Insert(0, label);
        return label;
    }
}
