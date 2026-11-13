using UniGetUI.Avalonia.Views;
using UniGetUI.Core.SettingsEngine;

namespace UniGetUI.Avalonia.Infrastructure;

internal static class NavigationPageOrder
{
    // Upkeep exposes only the software-updates page. The package engine and shared
    // UI infrastructure remain from UniGetUI, but other product pages are not navigable.
    public static PageType[] DefaultPages(
        bool catalogEnabled,
        bool gitRepositoriesEnabled = false,
        bool agentSkillsEnabled = false,
        bool xcodeEnabled = false) => [PageType.Updates];

    private static PageType[] AllPages => DefaultPages(
        catalogEnabled: true,
        gitRepositoriesEnabled: true,
        agentSkillsEnabled: true,
        xcodeEnabled: true);

    public static PageType[] Get(
        bool catalogEnabled,
        bool gitRepositoriesEnabled = false,
        bool agentSkillsEnabled = false,
        bool xcodeEnabled = false)
        => Resolve(
            Settings.GetValue(Settings.K.NavigationPageOrder),
            catalogEnabled,
            gitRepositoriesEnabled,
            agentSkillsEnabled,
            xcodeEnabled);

    public static void Save(IReadOnlyList<PageType> visiblePages)
        => Settings.SetValue(
            Settings.K.NavigationPageOrder,
            Serialize(Merge(
                Get(catalogEnabled: true, gitRepositoriesEnabled: true, agentSkillsEnabled: true, xcodeEnabled: true),
                visiblePages)));

    public static void Reset() => Settings.SetValue(Settings.K.NavigationPageOrder, "");

    internal static PageType[] Resolve(
        string stored,
        bool catalogEnabled,
        bool gitRepositoriesEnabled = false,
        bool agentSkillsEnabled = false,
        bool xcodeEnabled = false)
    {
        PageType[] defaults = DefaultPages(catalogEnabled, gitRepositoriesEnabled, agentSkillsEnabled, xcodeEnabled);
        PageType[] ordered = stored
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(name => Enum.TryParse(name, out PageType page) ? page : PageType.Null)
            .Where(defaults.Contains)
            .Distinct()
            .ToArray();
        return [.. ordered, .. defaults.Where(page => !ordered.Contains(page))];
    }

    internal static PageType[] Merge(PageType[] fullOrder, IReadOnlyList<PageType> visiblePages)
    {
        PageType[] merged = [.. fullOrder];
        int next = 0;
        for (int i = 0; i < merged.Length && next < visiblePages.Count; i++)
        {
            if (visiblePages.Contains(merged[i]))
                merged[i] = visiblePages[next++];
        }
        return merged;
    }

    // An order that is the default one for the pages it holds is stored as unset, so a page added later
    // finds it still following the default.
    internal static string Serialize(PageType[] fullOrder)
        => fullOrder.SequenceEqual(AllPages.Where(fullOrder.Contains)) ? "" : string.Join(',', fullOrder);
}
