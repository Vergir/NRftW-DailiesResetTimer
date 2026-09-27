using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine.UIElements;

namespace DailiesResetTimer;

/// <summary>UI Toolkit helpers that walk the physical hierarchy (template containers like ListView included).</summary>
internal static class UiTree
{
    public static List<T> FindAll<T>(VisualElement root, string name) where T : Il2CppObjectBase
    {
        var found = new List<T>();
        Walk(root, name, found);
        return found;
    }

    public static T? Find<T>(VisualElement root, string name) where T : Il2CppObjectBase
    {
        var all = FindAll<T>(root, name);
        return all.Count > 0 ? all[0] : null;
    }

    private static void Walk<T>(VisualElement e, string name, List<T> found) where T : Il2CppObjectBase
    {
        if (e.name == name)
        {
            var t = e.TryCast<T>();
            if (t != null) found.Add(t);
        }
        var h = e.hierarchy;
        int n = h.childCount;
        for (int i = 0; i < n; i++) Walk(h.ElementAt(i), name, found);
    }

    public static bool HasAncestorWithClass(VisualElement e, string className)
    {
        for (var p = e.hierarchy.parent; p != null; p = p.hierarchy.parent)
            if (p.ClassListContains(className)) return true;
        return false;
    }

    public static void SetText(TextElement label, string text)
    {
        if (label.text != text) label.text = text;
    }
}
