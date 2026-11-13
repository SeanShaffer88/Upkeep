using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using UniGetUI.Avalonia.Infrastructure;
using UniGetUI.Avalonia.ViewModels;
using UniGetUI.Avalonia.Views;
using UniGetUI.Core.Data;
using CoreSettings = UniGetUI.Core.SettingsEngine.Settings;

namespace UniGetUI.Tests;

public sealed class SidebarNavigationOrderTests : IDisposable
{
    private readonly HeadlessUnitTestSession _session =
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(SidebarNavigationOrderTests).Assembly);
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        nameof(SidebarNavigationOrderTests),
        Guid.NewGuid().ToString("N"));

    public SidebarNavigationOrderTests()
    {
        CoreData.TEST_DataDirectoryOverride = Path.Combine(_testRoot, "Data");
        Directory.CreateDirectory(CoreData.UniGetUIUserConfigurationDirectory);
        CoreSettings.ResetSettings();
    }

    public void Dispose()
    {
        CoreSettings.ResetSettings();
        CoreData.TEST_DataDirectoryOverride = null;
        Directory.Delete(_testRoot, recursive: true);
    }

    [Fact]
    public void UpkeepDefaultsToOnlySoftwareUpdates()
    {
        var sidebar = new SidebarViewModel();

        Assert.Equal([PageType.Updates], sidebar.NavigationPages);
    }

    [Fact]
    public void SavedUniGetUiPageOrderCannotExposeOtherPages()
    {
        CoreSettings.SetValue(CoreSettings.K.NavigationPageOrder, "Discover,Installed,Bundles,Updates");

        var sidebar = new SidebarViewModel();

        Assert.Equal([PageType.Updates], sidebar.NavigationPages);
    }

    [Fact]
    public Task SidebarOnlyShowsSoftwareUpdates() => _session.Dispatch(() =>
    {
        if (!Application.Current!.Styles.OfType<FluentTheme>().Any())
            Application.Current.Styles.Add(new FluentTheme());

        var sidebar = new SidebarViewModel();
        var view = new SidebarView { DataContext = sidebar };
        var window = new Window { Width = 300, Height = 700, Content = view };
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        ListBox list = view.FindControl<ListBox>("NavListBox")!;
        Assert.Equal(new string?[] { "Updates" }, list.Items.OfType<ListBoxItem>().Select(item => (string?)item.Tag));

        window.Close();
    }, CancellationToken.None);
}
