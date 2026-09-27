using System.Collections.Generic;
using Il2CppMoon.Forsaken;
using UnityEngine;
using UnityEngine.UIElements;

namespace DailiesResetTimer;

/// <summary>
/// Bounty board / challenge NPC screen (ActivityVendorWindow.uxml). Its Daily and Weekly list headers (".dailycount",
/// ".weeklycount": "HITS 0/4", "QUICK DARES 0/4"...) each have a ".daily-time-container" with an hourglass and a Label
/// named "daily-reset-time-label". The game's stylesheet (ActivityVendorWindow.uss) styles that container as
/// "position: absolute; width: 100px; height: 40px; top: -12%; right: -18%" and then hides it (display: none); the game
/// fills only the daily label, once per Show, from the local clock. We fill both from the simulation's schedule and
/// show the container centered just above its header, where the board is empty (the header row has no room: the list's
/// "Daily" / "Weekly" title sits right below it).
/// Bounties and challenges are two separate ActivityVendorScreenPanels (the tabs switch between them); the static
/// ActivityVendorScreenPanel.Instance is only one of them, so every panel is checked.
/// </summary>
internal static class VendorScreenTimers
{
    private const string LabelName = "daily-reset-time-label";
    private const string WeeklyHeaderClass = "weeklycount";
    private const string ShownMarker = "drt-shown";   // our inline styles are set on this container
    private const string Placeholder = "--:--";
    private const float RescanInterval = 2f;

    private static readonly List<ActivityVendorScreenPanel> _panels = new List<ActivityVendorScreenPanel>();
    private static float _nextRescan;

    public static void Update()
    {
        if (Time.unscaledTime >= _nextRescan || _panels.Exists(p => p == null))
        {
            _nextRescan = Time.unscaledTime + RescanInterval;
            _panels.Clear();
            foreach (var p in Resources.FindObjectsOfTypeAll<ActivityVendorScreenPanel>())
                if (p != null && p.gameObject.scene.IsValid()) _panels.Add(p);
        }
        foreach (var p in _panels)
        {
            var screen = p != null ? p.Screen : null;
            if (screen != null && screen.IsVisible) UpdateScreen(screen);
        }
    }

    private static void UpdateScreen(ActivityVendorScreen screen)
    {
        var root = screen.m_screenRootElement;
        if (root == null) return;
        if (!ResetClock.TryGet(screen.m_activityType, out var times)) return;

        foreach (var label in UiTree.FindAll<Label>(root, LabelName))
        {
            bool weekly = UiTree.HasAncestorWithClass(label, WeeklyHeaderClass);
            UiTree.SetText(label, ResetClock.Format(weekly ? times.Weekly : times.Daily));
            var container = label.hierarchy.parent;
            if (container != null && !container.ClassListContains(ShownMarker)) Show(container, label);
        }
    }

    /// <summary>Unload / hot reload / switched off: hide the containers again and put back the placeholder.</summary>
    public static void Restore()
    {
        foreach (var p in Resources.FindObjectsOfTypeAll<ActivityVendorScreenPanel>())
        {
            var root = p != null ? p.Screen?.m_screenRootElement : null;
            if (root == null) continue;
            foreach (var label in UiTree.FindAll<Label>(root, LabelName))
            {
                if (UiTree.HasAncestorWithClass(label, WeeklyHeaderClass)) label.text = Placeholder;
                var container = label.hierarchy.parent;
                if (container != null && container.ClassListContains(ShownMarker)) Hide(container, label);
            }
        }
        _panels.Clear();
    }

    private static void Show(VisualElement container, Label label)
    {
        // The UXML gives the label an inline "flex: 1" (grow 1, basis 0). Growing, it stretches across the header, where
        // the daily and weekly stylesheets align its text differently (left vs right); not growing, basis 0 leaves it
        // 3 units wide with the text spilling out of it, which throws off the centering. Sized to its text, hourglass and
        // time form one group centered over the header like the title below it.
        label.style.flexGrow = new StyleFloat(0f);
        label.style.flexBasis = new StyleLength(StyleKeyword.Auto);
        label.style.unityTextAlign = new StyleEnum<TextAnchor>(TextAnchor.MiddleLeft);
        var s = container.style;
        s.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
        s.position = new StyleEnum<Position>(Position.Absolute);
        s.left = new StyleLength(0f);
        s.right = new StyleLength(0f);
        s.top = new StyleLength(StyleKeyword.Auto);                         // USS: top -12%
        s.bottom = new StyleLength(new Length(100f, LengthUnit.Percent));
        s.width = new StyleLength(StyleKeyword.Auto);                       // USS: 100px, too narrow for "5d 08h"
        s.marginRight = new StyleLength(0f);                                // USS: 20px
        s.flexDirection = new StyleEnum<FlexDirection>(FlexDirection.Row);
        s.justifyContent = new StyleEnum<Justify>(Justify.Center);
        s.alignItems = new StyleEnum<Align>(Align.Center);
        container.AddToClassList(ShownMarker);
    }

    private static void Hide(VisualElement container, Label label)
    {
        label.style.flexGrow = new StyleFloat(1f);   // back to the UXML's inline "flex: 1"
        label.style.flexBasis = new StyleLength(0f);
        label.style.unityTextAlign = new StyleEnum<TextAnchor>(StyleKeyword.Null);
        var s = container.style;
        s.display = new StyleEnum<DisplayStyle>(StyleKeyword.Null);
        s.position = new StyleEnum<Position>(StyleKeyword.Null);
        s.left = new StyleLength(StyleKeyword.Null);
        s.right = new StyleLength(StyleKeyword.Null);
        s.top = new StyleLength(StyleKeyword.Null);
        s.bottom = new StyleLength(StyleKeyword.Null);
        s.width = new StyleLength(StyleKeyword.Null);
        s.marginRight = new StyleLength(StyleKeyword.Null);
        s.flexDirection = new StyleEnum<FlexDirection>(StyleKeyword.Null);
        s.justifyContent = new StyleEnum<Justify>(StyleKeyword.Null);
        s.alignItems = new StyleEnum<Align>(StyleKeyword.Null);
        container.RemoveFromClassList(ShownMarker);
    }
}
