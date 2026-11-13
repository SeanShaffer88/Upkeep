using System.Diagnostics;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using UniGetUI.PackageEngine.Interfaces;
using UniGetUI.PackageEngine.PackageLoader;
using Thickness = global::Avalonia.Thickness;

namespace UniGetUI.Avalonia.Views.Pages;

/// <summary>
/// Scans user-selected folders for portable executables and presents WinGet search results
/// as review-only candidates. It never installs a search result or changes a scanned folder.
/// </summary>
internal sealed class PortableAppsScanWindow : Window
{
    private const int MaximumExecutables = 20_000;
    private const int MaximumWinGetSearches = 25;

    private readonly StackPanel _folderRows = new() { Spacing = 4 };
    private readonly StackPanel _resultRows = new() { Spacing = 8 };
    private readonly TextBlock _status = new()
    {
        Text = "Add folders that contain portable applications, then scan them.",
        TextWrapping = TextWrapping.Wrap,
    };
    private readonly List<string> _folders = LoadFolders().ToList();

    public PortableAppsScanWindow()
    {
        Title = "Portable application scan";
        Width = 900;
        Height = 680;
        MinWidth = 650;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new StackPanel
        {
            Margin = new Thickness(22),
            Spacing = 12,
        };
        root.Children.Add(new TextBlock
        {
            Text = "Find portable applications",
            FontSize = 22,
            FontWeight = FontWeight.SemiBold,
        });
        root.Children.Add(new TextBlock
        {
            Text = "Choose folders to inspect. Upkeep reads executable version details and suggests possible WinGet matches for you to review.",
            TextWrapping = TextWrapping.Wrap,
        });

        var folderActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
        };
        var addFolder = new Button { Content = "Add folder" };
        addFolder.Click += async (_, _) => await AddFolderAsync();
        var scan = new Button { Content = "Scan folders" };
        scan.Click += async (_, _) => await ScanAsync(scan);
        var portableFreeware = new Button { Content = "PortableFreeware reference" };
        portableFreeware.Click += (_, _) => OpenExternalUrl("https://portablefreeware.com/");
        folderActions.Children.Add(addFolder);
        folderActions.Children.Add(scan);
        folderActions.Children.Add(portableFreeware);
        root.Children.Add(folderActions);

        root.Children.Add(new TextBlock
        {
            Text = "Folders",
            FontWeight = FontWeight.SemiBold,
        });
        root.Children.Add(new Border
        {
            Padding = new Thickness(8),
            BorderThickness = new Thickness(1),
            Child = new ScrollViewer
            {
                MaxHeight = 115,
                Content = _folderRows,
            },
        });
        root.Children.Add(_status);
        root.Children.Add(new TextBlock
        {
            Text = "Portable application candidates",
            FontWeight = FontWeight.SemiBold,
        });
        root.Children.Add(new Border
        {
            Padding = new Thickness(8),
            BorderThickness = new Thickness(1),
            Child = new ScrollViewer
            {
                Height = 340,
                Content = _resultRows,
            },
        });
        root.Children.Add(new TextBlock
        {
            Text = "WinGet results are suggestions only. Updates remain in the main Software Updates list; Upkeep will not replace files based on a name match.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
        });

        Content = root;
        RenderFolders();
    }

    private async Task AddFolderAsync()
    {
        var selected = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Choose a portable application folder",
        });
        if (selected.Count == 0)
            return;

        string path = selected[0].Path.LocalPath;
        if (!Directory.Exists(path)
            || _folders.Any(existing => string.Equals(existing, path, StringComparison.OrdinalIgnoreCase)))
            return;

        _folders.Add(path);
        SaveFolders(_folders);
        RenderFolders();
    }

    private void RemoveFolder(string path)
    {
        _folders.Remove(path);
        SaveFolders(_folders);
        RenderFolders();
    }

    private async Task ScanAsync(Button scanButton)
    {
        if (_folders.Count == 0)
        {
            _status.Text = "Add at least one folder before scanning.";
            return;
        }

        scanButton.IsEnabled = false;
        _resultRows.Children.Clear();
        _status.Text = "Scanning selected folders and checking possible WinGet matches…";

        try
        {
            var results = await Task.Run(ScanAndMatch);
            foreach (var result in results.Rows)
                _resultRows.Children.Add(CreateResultCard(result));
            _status.Text = results.Summary;
        }
        catch (Exception ex)
        {
            _status.Text = $"The scan could not be completed: {ex.Message}";
        }
        finally
        {
            scanButton.IsEnabled = true;
        }
    }

    private ScanResult ScanAndMatch()
    {
        var applications = ScanExecutables(_folders);
        var manager = UpgradablePackagesLoader.Instance?.FindManager("WinGet");
        var updates = UpgradablePackagesLoader.Instance?.Packages ?? [];
        var rows = new List<PortableApplicationRow>();
        int searches = 0;

        foreach (var application in applications)
        {
            string matchDescription;
            if (manager is null || !manager.IsReady())
            {
                matchDescription = "WinGet is unavailable; no package match was checked.";
            }
            else if (searches >= MaximumWinGetSearches)
            {
                matchDescription = $"Search limit reached; review this application manually.";
            }
            else
            {
                searches++;
                matchDescription = FindWinGetMatches(manager, updates, application);
            }

            rows.Add(application with { MatchDescription = matchDescription });
        }

        string summary = applications.Count == 0
            ? "No executable applications were found in the selected folders."
            : $"Found {applications.Count} executable file(s). Checked up to {searches} names against WinGet.";
        if (applications.Count >= MaximumExecutables)
            summary += $" Scanning stopped at the {MaximumExecutables:N0}-file safety limit.";

        return new ScanResult(rows, summary);
    }

    private static List<PortableApplicationRow> ScanExecutables(IReadOnlyList<string> folders)
    {
        var rows = new List<PortableApplicationRow>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<(string Path, int Depth)>();
        foreach (string folder in folders)
            if (Directory.Exists(folder))
                pending.Enqueue((folder, 0));

        while (pending.Count > 0 && rows.Count <= MaximumExecutables)
        {
            var (directory, depth) = pending.Dequeue();
            string normalized;
            try
            {
                normalized = Path.GetFullPath(directory);
                if (!visited.Add(normalized)
                    || (File.GetAttributes(normalized) & FileAttributes.ReparsePoint) != 0)
                    continue;

                foreach (string file in Directory.EnumerateFiles(normalized, "*.exe", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        var details = FileVersionInfo.GetVersionInfo(file);
                        string name = FirstNonEmpty(details.ProductName, details.FileDescription, Path.GetFileNameWithoutExtension(file));
                        rows.Add(new PortableApplicationRow(
                            name,
                            string.IsNullOrWhiteSpace(details.FileVersion) ? "Unknown version" : details.FileVersion,
                            file,
                            ""));
                        if (rows.Count >= MaximumExecutables)
                            break;
                    }
                    catch
                    {
                        // An unreadable or changing executable should not stop the rest of the scan.
                    }
                }

                if (depth >= 8 || rows.Count >= MaximumExecutables)
                    continue;

                foreach (string child in Directory.EnumerateDirectories(normalized, "*", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0)
                            pending.Enqueue((child, depth + 1));
                    }
                    catch
                    {
                        // Skip inaccessible or removed directories.
                    }
                }
            }
            catch
            {
                // Skip inaccessible or removed folders and continue with the remaining paths.
            }
        }

        return rows;
    }

    private static string FindWinGetMatches(
        IPackageManager manager,
        IReadOnlyList<IPackage> availableUpdates,
        PortableApplicationRow application)
    {
        try
        {
            var packages = manager.FindPackages(application.Name, out bool failed);
            if (failed || packages.Count == 0)
                return "No WinGet result found; verify the vendor and source manually.";

            string key = Normalize(application.Name);
            var candidates = packages
                .Select(package => (Package: package, NameKey: Normalize(package.Name)))
                .Where(candidate =>
                    candidate.NameKey == key
                    || (key.Length >= 5 && candidate.NameKey.Contains(key, StringComparison.Ordinal))
                    || (candidate.NameKey.Length >= 5 && key.Contains(candidate.NameKey, StringComparison.Ordinal)))
                .Take(3)
                .ToList();

            if (candidates.Count == 0)
                return "No close name match found; verify the vendor and source manually.";

            return string.Join(
                Environment.NewLine,
                candidates.Select(candidate =>
                {
                    var update = availableUpdates.FirstOrDefault(package =>
                        package.Manager.Id.Equals(manager.Id, StringComparison.OrdinalIgnoreCase)
                        && package.Id.Equals(candidate.Package.Id, StringComparison.OrdinalIgnoreCase));
                    string updateText = update is null
                        ? "no update currently listed"
                        : $"update listed: {update.VersionString} → {update.NewVersionString}";
                    return $"{candidate.Package.Name} ({candidate.Package.Id}) — {updateText}";
                }));
        }
        catch (Exception ex)
        {
            return $"WinGet search failed: {ex.Message}";
        }
    }

    private Control CreateResultCard(PortableApplicationRow application)
    {
        var text = new TextBlock
        {
            Text = $"{application.Name} · {application.Version}{Environment.NewLine}{application.Path}{Environment.NewLine}{application.MatchDescription}",
            TextWrapping = TextWrapping.Wrap,
        };
        var topicSearches = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
        };
        foreach (string topic in new[] { "portable", "portableapps", "portable-app", "portable-apps", "portableapps.com" })
        {
            var search = new Button { Content = topic };
            search.Click += (_, _) => OpenGitHubTopicSearch(application.Name, topic);
            topicSearches.Children.Add(search);
        }

        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(text);
        content.Children.Add(topicSearches);
        return new Border
        {
            Padding = new Thickness(10),
            BorderThickness = new Thickness(1),
            Child = content,
        };
    }

    private void OpenGitHubTopicSearch(string appName, string topic)
    {
        string query = Uri.EscapeDataString($"{appName} topic:{topic}");
        OpenExternalUrl($"https://github.com/search?q={query}&type=repositories");
    }

    private void OpenExternalUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _status.Text = $"Could not open the reference link: {ex.Message}";
        }
    }

    private void RenderFolders()
    {
        _folderRows.Children.Clear();
        foreach (string folder in _folders)
        {
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            };
            row.Children.Add(new TextBlock
            {
                Text = folder,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
            });
            var remove = new Button
            {
                Content = "Remove",
                Tag = folder,
                Margin = new Thickness(8, 0, 0, 0),
            };
            remove.Click += (_, _) => RemoveFolder((string)remove.Tag!);
            Grid.SetColumn(remove, 1);
            row.Children.Add(remove);
            _folderRows.Children.Add(row);
        }
    }

    private static string[] LoadFolders()
    {
        try
        {
            if (File.Exists(FolderConfigPath))
                return JsonSerializer.Deserialize<string[]>(File.ReadAllText(FolderConfigPath)) ?? [];
        }
        catch
        {
            // A damaged settings file should not block scanning; users can add folders again.
        }

        return [];
    }

    private static void SaveFolders(IEnumerable<string> folders)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FolderConfigPath)!);
        File.WriteAllText(FolderConfigPath, JsonSerializer.Serialize(folders.Distinct(StringComparer.OrdinalIgnoreCase)));
    }

    private static string FolderConfigPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Upkeep",
            "portable-folders.json");

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "Unknown application";

    private static string Normalize(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private sealed record PortableApplicationRow(
        string Name,
        string Version,
        string Path,
        string MatchDescription);

    private sealed record ScanResult(IReadOnlyList<PortableApplicationRow> Rows, string Summary);
}
