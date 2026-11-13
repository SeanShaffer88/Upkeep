using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using UniGetUI.PackageEngine.PackageLoader;
using Thickness = global::Avalonia.Thickness;

namespace UniGetUI.Avalonia.Views.Pages;

/// <summary>
/// Exports the package-manager inventory and compares an imported Upkeep inventory
/// without installing, removing, or changing any software.
/// </summary>
internal static class ApplicationInventoryTools
{
    private const int CurrentFormatVersion = 1;
    private static readonly InventoryJsonContext JsonContext = new(new JsonSerializerOptions
    {
        WriteIndented = true,
    });

    public static async Task ExportAsync(Window owner)
    {
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export installed application inventory",
            SuggestedFileName = "upkeep-inventory.json",
            DefaultExtension = "json",
            FileTypeChoices = [new FilePickerFileType("Upkeep inventory") { Patterns = ["*.json"] }],
        });
        if (file is null)
            return;

        var packages = InstalledPackagesLoader.Instance?.Packages ?? [];
        var inventory = new InventorySnapshot(
            CurrentFormatVersion,
            DateTimeOffset.UtcNow,
            packages
                .Select(package => new InventoryEntry(
                    package.Manager.Name,
                    package.Id,
                    package.Name,
                    package.VersionString))
                .DistinctBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray());

        await using var stream = await file.OpenWriteAsync();
        await JsonSerializer.SerializeAsync(stream, inventory, JsonContext.InventorySnapshot);
    }

    public static async Task CompareAsync(Window owner)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose an Upkeep inventory to compare",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Upkeep inventory") { Patterns = ["*.json"] }],
        });
        if (files.Count == 0)
            return;

        InventorySnapshot? imported;
        try
        {
            await using var stream = await files[0].OpenReadAsync();
            imported = await JsonSerializer.DeserializeAsync(stream, JsonContext.InventorySnapshot);
        }
        catch (Exception ex)
        {
            await ShowReportAsync(owner, "Could not read inventory", ex.Message);
            return;
        }

        if (imported is null || imported.FormatVersion != CurrentFormatVersion)
        {
            await ShowReportAsync(owner, "Unsupported inventory", "This file is not a supported Upkeep inventory export.");
            return;
        }

        var localPackages = InstalledPackagesLoader.Instance?.Packages ?? [];
        var local = localPackages
            .Select(package => new InventoryEntry(
                package.Manager.Name,
                package.Id,
                package.Name,
                package.VersionString))
            .ToDictionary(entry => entry.Key, StringComparer.OrdinalIgnoreCase);
        var remote = imported.Applications
            .ToDictionary(entry => entry.Key, StringComparer.OrdinalIgnoreCase);

        var differences = new List<string>();
        foreach (var pair in remote.OrderBy(pair => pair.Value.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (!local.TryGetValue(pair.Key, out var localEntry))
            {
                differences.Add($"Not installed on this PC: {pair.Value.Name} ({pair.Value.Version})");
            }
            else if (!string.Equals(localEntry.Version, pair.Value.Version, StringComparison.OrdinalIgnoreCase))
            {
                differences.Add($"{pair.Value.Name}: this PC {localEntry.Version}; imported {pair.Value.Version}");
            }
        }

        foreach (var pair in local.OrderBy(pair => pair.Value.Name, StringComparer.OrdinalIgnoreCase))
            if (!remote.ContainsKey(pair.Key))
                differences.Add($"Only on this PC: {pair.Value.Name} ({pair.Value.Version})");

        string summary =
            $"Imported snapshot from {imported.ExportedAt.LocalDateTime:g}. " +
            $"{remote.Count} imported package(s); {local.Count} package(s) on this PC. " +
            (differences.Count == 0 ? "The inventories match." : $"{differences.Count} difference(s) found.");
        string details = differences.Count == 0
            ? "No package or version differences were found."
            : string.Join(Environment.NewLine, differences.Take(300));
        if (differences.Count > 300)
            details += $"{Environment.NewLine}…and {differences.Count - 300} more difference(s).";

        await ShowReportAsync(owner, "Inventory comparison", $"{summary}{Environment.NewLine}{Environment.NewLine}{details}");
    }

    private static async Task ShowReportAsync(Window owner, string title, string report)
    {
        var close = new Button
        {
            Content = "Close",
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 96,
        };
        var dialog = new Window
        {
            Title = title,
            Width = 740,
            Height = 560,
            MinWidth = 520,
            MinHeight = 380,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        close.Click += (_, _) => dialog.Close();
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = title, FontSize = 20, FontWeight = FontWeight.SemiBold },
                new Border
                {
                    Padding = new Thickness(10),
                    BorderThickness = new Thickness(1),
                    Child = new ScrollViewer
                    {
                        Height = 430,
                        Content = new TextBlock { Text = report, TextWrapping = TextWrapping.Wrap },
                    },
                },
                close,
            },
        };
        await dialog.ShowDialog(owner);
    }

    internal sealed record InventorySnapshot(
        int FormatVersion,
        DateTimeOffset ExportedAt,
        InventoryEntry[] Applications);

    internal sealed record InventoryEntry(
        string Manager,
        string Id,
        string Name,
        string Version)
    {
        public string Key => $"{Manager}|{Id}";
    }
}

[JsonSerializable(typeof(ApplicationInventoryTools.InventorySnapshot))]
internal sealed partial class InventoryJsonContext : JsonSerializerContext;
