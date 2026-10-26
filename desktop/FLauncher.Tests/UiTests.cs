using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FLauncher.Desktop;
using FLauncher.Core;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(FLauncher.Tests.TestApp))]
namespace FLauncher.Tests;
public static class TestApp
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
public sealed class UiTests
{
    [AvaloniaFact]
    public void WelcomeAndAllPagesRender()
    {
        var window = new MainWindow(new LauncherService(new LauncherSettings { DataPath = Path.Combine(Path.GetTempPath(), "flauncher-empty-ui-fixture") })); window.Show(); Dispatcher.UIThread.RunJobs();
        var welcome = Assert.Single(window.OwnedWindows);
        Assert.Equal("Welcome to FLauncher", welcome.Title);
        var artifacts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts/ui"));
        Directory.CreateDirectory(artifacts);
        using (var frame = welcome.CaptureRenderedFrame()) { Assert.NotNull(frame); frame.Save(Path.Combine(artifacts, "welcome.png")); }
        welcome.Close(); Dispatcher.UIThread.RunJobs();
        foreach (var button in window.FindControl<StackPanel>("Navigation")!.Children.OfType<Button>())
        {
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs();
            Assert.NotEmpty(window.FindControl<StackPanel>("Page")!.Children);
            Assert.Empty(window.OwnedWindows);
            using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame);
            frame.Save(Path.Combine(artifacts, button.Tag + ".png"));
        }
        window.Close();
    }
    [AvaloniaFact]
    public void BundledSettingsDoNotAskForExternalEngines()
    {
        var root = Path.Combine(Path.GetTempPath(), "flauncher-ui-bundle-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "backends", "java"));
        Directory.CreateDirectory(Path.Combine(root, "backends", "bedrock"));
        File.WriteAllText(Path.Combine(root, "backends", "java", "prismlauncher.exe"), "fixture");
        File.WriteAllText(Path.Combine(root, "backends", "bedrock", "LeviLauncher.exe"), "fixture");
        var window = new MainWindow(new LauncherService(new LauncherSettings { DataPath = Path.Combine(root, "data") }, root));
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            Assert.Single(window.OwnedWindows).Close(); Dispatcher.UIThread.RunJobs();
            var settings = window.FindControl<StackPanel>("Navigation")!.Children.OfType<Button>().Single(b => Equals(b.Tag, "Settings"));
            settings.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs();
            var page = window.FindControl<StackPanel>("Page")!;
            Assert.DoesNotContain(page.Children, child => child is Expander);
            var artifacts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts/ui"));
            Directory.CreateDirectory(artifacts);
            using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame); frame.Save(Path.Combine(artifacts, "BundledSettings.png"));
        }
        finally { window.Close(); Directory.Delete(root, true); }
    }
}
