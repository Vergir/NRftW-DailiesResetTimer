using Il2CppMoon.Forsaken;
using Il2CppMoon.TheLoop;
using Il2CppQuantum;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DailiesResetTimer;

/// <summary>
/// Shops with limited stock (LimitedStockVendorScreen, a uGUI screen). Each restockable vendor has a restock activity
/// that VendorInventoryRestockActivitySystem serves every day (all of them, DailyActivityCount = int.MaxValue); serving
/// it runs VendorInventoryRestockDirector.TryRestockVendor, which refills the vendor. The screen has no restock text of
/// its own, so we add one above the top-right corner of the vendor's item panel.
/// </summary>
internal static class ShopTimer
{
    private const string LabelName = "DRT_RestockTimer";
    private const float RescanInterval = 2f;

    private static LimitedStockVendorScreen? _screen;
    private static float _nextRescan;

    public static void Update()
    {
        var screen = FindScreen();
        var panel = screen != null ? screen.VendorInventory : null;
        if (panel == null) return;
        var label = FindLabel(panel.transform);
        // The vendor panel is shared with other vendor screens: only show while this one is open.
        if (!screen!.gameObject.activeInHierarchy)
        {
            if (label != null && label.gameObject.activeSelf) label.gameObject.SetActive(false);
            return;
        }

        label ??= Create(panel);
        bool show = ResetClock.TryGet(ActivityType.VendorInventoryRestock, out var times) && VendorRestocks(screen);
        if (label.gameObject.activeSelf != show) label.gameObject.SetActive(show);
        if (show)
        {
            string text = "Restocks in " + ResetClock.Format(times.Daily);
            if (label.text != text) label.text = text;
        }
    }

    /// <summary>Unload / hot reload / switched off: destroy the label.</summary>
    public static void Remove()
    {
        foreach (var s in Resources.FindObjectsOfTypeAll<LimitedStockVendorScreen>())
        {
            var panel = s != null ? s.VendorInventory : null;
            var label = panel != null ? FindLabel(panel.transform) : null;
            if (label != null) Object.DestroyImmediate(label.gameObject);
        }
        _screen = null;
    }

    /// <summary>The vendor on screen has a restock activity (the game's own lookup).</summary>
    private static bool VendorRestocks(LimitedStockVendorScreen screen)
    {
        var frame = ViewFrame.s_activeViewFrame?.GetQtn();
        return frame != null && screen.FindRestockActivity(frame) != null;
    }

    private static LimitedStockVendorScreen? FindScreen()
    {
        if (_screen != null) return _screen;
        if (Time.unscaledTime < _nextRescan) return null;
        _nextRescan = Time.unscaledTime + RescanInterval;
        foreach (var s in Resources.FindObjectsOfTypeAll<LimitedStockVendorScreen>())
            if (s != null && s.gameObject.scene.IsValid()) { _screen = s; break; }
        return _screen;
    }

    // Live layout (vendorInventoryElementShared): the vendor tab "vendorTabShared" (430 x 35, top of the panel) holds
    // "title/text" = "Vendor" (28 pt) with its bottom edge 1 unit above the tab's top. The label sits on that line, right-aligned,
    // a little higher so its descenders clear the first row of item slots.
    private const string TabPath = "vendorTabShared", TitlePath = "vendorTabShared/title/text";

    private static TextMeshProUGUI? FindLabel(Transform panel)
    {
        var tab = panel.Find(TabPath) ?? panel;
        var t = tab.Find(LabelName);
        return t != null ? t.GetComponent<TextMeshProUGUI>() : null;
    }

    private static TextMeshProUGUI Create(InventoryItemsElement panel)
    {
        var tab = panel.transform.Find(TabPath) ?? panel.transform;
        var go = new GameObject(LabelName);
        go.layer = tab.gameObject.layer;
        var rt = go.AddComponent<RectTransform>();
        // Unity 6 has only the 3-argument native SetParent; the interop's 2-argument one is stripped.
        rt.SetParent(tab, false, false);
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(0f, 12f);   // clear of the first row of slots
        rt.sizeDelta = new Vector2(400f, 27f);
        rt.localScale = Vector3.one;
        go.AddComponent<LayoutElement>().ignoreLayout = true;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        var titleT = panel.transform.Find(TitlePath);
        var title = titleT != null ? titleT.GetComponent<TextMeshProUGUI>() : panel.GetComponentInChildren<TextMeshProUGUI>(true);
        if (title != null)
        {
            tmp.font = title.font;
            tmp.fontSharedMaterial = title.fontSharedMaterial;
            tmp.color = title.color;
        }
        tmp.fontSize = 22f;
        tmp.alignment = TextAlignmentOptions.BottomRight;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;
        return tmp;
    }
}
