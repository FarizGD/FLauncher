// SPDX-License-Identifier: GPL-3.0-only
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using FLauncher.Core;

namespace FLauncher.Desktop;

public partial class MainWindow : Window
{
    private LauncherService service = null!;
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromMinutes(20) };
    private CancellationTokenSource? installation;
    private string currentPage = "Play";
    private bool bedrock;
    private GameInstance? selected;
    private IReadOnlyList<BedrockApp> bedrockApps = [];
    private const string Attribution = "FLauncher brings together Prism Launcher for Java and LeviLauncher for Bedrock in one distribution, with an original Avalonia interface inspired by LeviLauncher. Credit and all rights to their respective work belong to Prism Launcher, LeviLauncher, LiteDev (LiteLDev), and their contributors. Minecraft belongs to Mojang and Microsoft. This independent fork is not affiliated with or endorsed by those projects.";

    public MainWindow() : this(null) { }
    public MainWindow(LauncherService? launcherService)
    {
        InitializeComponent();
        Opened += async (_, _) =>
        {
            try { service = launcherService ?? new LauncherService(); Render(); await Welcome(); }
            catch (Exception ex)
            {
                service = new LauncherService(new LauncherSettings());
                currentPage = "Settings"; Render();
                await Message("Startup needs attention", ex.Message + "\nYou can correct your paths in Settings. Existing settings are only replaced when you save.");
            }
        };
        Closing += (_, e) =>
        {
            if (installation is not null) { e.Cancel = true; installation.Cancel(); Status.Text = "Cancelling installation. Close again when cleanup finishes."; }
        };
        Closed += (_, _) => http.Dispose();
        Activated += (_, _) =>
        {
            // Refresh the library after returning from either included manager.
            if (service is not null && OwnedWindows.Count == 0 && currentPage is "Play" or "Library") Guard(Render);
        };
    }
    private void Navigate(object? sender, RoutedEventArgs e)
    {
        if (service is null) return;
        currentPage = (string)((Button)sender!).Tag!;
        foreach (var button in this.FindControl<StackPanel>("Navigation")!.Children.OfType<Button>())
            button.Classes.Set("active", (string)button.Tag! == currentPage);
        Guard(Render);
    }
    private void Render()
    {
        Page.Children.Clear();
        switch (currentPage)
        {
            case "Settings": Settings(); break;
            case "Credits": Credits(); break;
            case "Java": Java(); break;
            case "Library": Library(); break;
            default: Play(); break;
        }
    }
    private static TextBlock Text(string text, int size = 14, bool muted = false)
    {
        var block = new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap };
        if (muted) block.Classes.Add("muted");
        return block;
    }
    private static StackPanel Stack(params Control[] children)
    {
        var stack = new StackPanel { Spacing = 14 };
        foreach (var child in children) stack.Children.Add(child);
        return stack;
    }
    private static Border Card(Control child) { var card = new Border { Child = child }; card.Classes.Add("card"); return card; }
    private Button Action(string label, Action action, bool primary = false)
    {
        var button = new Button { Content = label };
        if (primary) button.Classes.Add("primary");
        button.Click += (_, _) => Guard(action);
        return button;
    }
    private async void Guard(Action action)
    {
        try { action(); }
        catch (Exception ex) { Status.Text = ex.Message; await Message("Something needs attention", ex.Message); }
    }
    private void Heading(string title, string subtitle) { Page.Children.Add(Text(title, 30)); Page.Children.Add(Text(subtitle, 14, true)); }
    private void Play()
    {
        Heading("Make yourself at home.", "Pick an edition. Find your world. Make it yours.");
        var editions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        editions.Children.Add(Action("Java Edition", () => { bedrock = false; Render(); }, !bedrock));
        editions.Children.Add(Action("Bedrock Edition", () => { bedrock = true; Render(); _ = DiscoverBedrock(); }, bedrock));
        Page.Children.Add(editions);
        var hero = Stack(Text(bedrock ? "BUILD TOGETHER" : "A WORLD OF POSSIBILITIES", 11),
            Text(bedrock ? "Your friends. Your worlds.\nYour Bedrock." : "Big ideas start\nwith a single block.", 38),
            Text(bedrock ? "Jump into your installed Minecraft or Preview on Windows." : "Vanilla adventures, modded worlds, and everything in between.", 15, true));
        var heroCard = Card(hero);
        heroCard.Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative), GradientStops = new GradientStops { new(Color.Parse("#254737"), 0), new(Color.Parse("#1D2B37"), 1) } };
        Page.Children.Add(heroCard);
        if (bedrock)
        {
            var content = Stack(Text("Minecraft for Windows", 20), Text("Requires a licensed installation. Release and Preview appear after discovery.", 13, true));
            foreach (var instance in service.GetBedrockInstances())
                content.Children.Add(Action($"▶  {instance.Name} · {instance.Version}", () => { service.StartLevi(instance.Name); Status.Text = "Launch requested for " + instance.Name; }, true));
            foreach (var app in bedrockApps) content.Children.Add(Action("▶  " + app.Name, () => { LauncherService.LaunchBedrock(app); Status.Text = "Launch requested for " + app.Name; }, true));
            if (bedrockApps.Count == 0) content.Children.Add(Text("No Bedrock apps detected yet. Refresh or install through the Microsoft Store.", 13, true));
            content.Children.Add(Action("Refresh installed editions", () => _ = DiscoverBedrock()));
            content.Children.Add(Action("Get Minecraft in Microsoft Store ↗", () => LauncherService.Open("ms-windows-store://pdp/?ProductId=9NBLGGH2JHXJ")));
            content.Children.Add(Action("Install versions & manage mods", () => service.StartLevi(), true));
            content.Children.Add(Text("Bedrock installation and advanced mod tools use the included LeviLauncher manager.", 12, true));
            Page.Children.Add(Card(content));
        }
        else
        {
            var instances = service.GetInstances();
            selected = instances.FirstOrDefault(i => i.Id == selected?.Id) ?? instances.FirstOrDefault();
            var combo = new ComboBox { ItemsSource = instances.Select(i => i.Name).ToArray(), SelectedIndex = selected is null ? -1 : instances.ToList().FindIndex(i => i.Id == selected.Id), HorizontalAlignment = HorizontalAlignment.Stretch, PlaceholderText = "Choose an instance" };
            combo.SelectionChanged += (_, _) => selected = combo.SelectedIndex >= 0 ? instances[combo.SelectedIndex] : null;
            var launch = Action("▶  Let's play", () => { if (selected is not null) { service.StartEngine(selected.Id); Status.Text = "Launch requested for " + selected.Name; } }, true);
            launch.IsEnabled = selected is not null;
            Page.Children.Add(Card(Stack(Text("Your next adventure", 20), combo, launch,
                Action("Add instances / manage accounts", () => service.StartEngine()))));
            if (instances.Count == 0) Page.Children.Add(Text("Your library is empty. Add an instance and sign in with the included Java manager to get started.", 13, true));
        }
    }
    private async Task DiscoverBedrock()
    {
        Status.Text = "Looking for installed Bedrock editions…";
        try { bedrockApps = await LauncherService.FindBedrockAsync(); Status.Text = $"Found {bedrockApps.Count} Bedrock edition(s)."; if (currentPage == "Play" && bedrock) Render(); }
        catch (Exception ex) { Status.Text = ex.Message; await Message("Bedrock discovery", ex.Message); }
    }
    private void Library()
    {
        Heading("Your library", "Your Java and Bedrock instances, together in one place.");
        Page.Children.Add(Action("Refresh library", Render));
        var search = new TextBox { Watermark = "Search your instances…" };
        var list = Stack(); var instances = service.GetInstances(); var bedrockInstances = service.GetBedrockInstances();
        void Filter()
        {
            list.Children.Clear();
            foreach (var instance in instances.Where(i => i.Name.Contains(search.Text ?? "", StringComparison.OrdinalIgnoreCase)))
                list.Children.Add(Card(Stack(Text(instance.Name, 20), Text(instance.Version, 12, true),
                    Action("▶  Play", () => service.StartEngine(instance.Id), true), Action("Open instance folder", () => LauncherService.Open(instance.Directory)))));
            foreach (var instance in bedrockInstances.Where(i => i.Name.Contains(search.Text ?? "", StringComparison.OrdinalIgnoreCase)))
                list.Children.Add(Card(Stack(Text(instance.Name, 20), Text($"Bedrock · {instance.Version} · {instance.Channel}", 12, true),
                    Action("▶  Play", () => service.StartLevi(instance.Name), true), Action("Open instance folder", () => LauncherService.Open(instance.Directory)))));
            if (list.Children.Count == 0) list.Children.Add(Text("No matching instances. Add one in the instance manager or select your existing data folder in Settings.", 14, true));
        }
        search.TextChanged += (_, _) => Filter(); Filter();
        Page.Children.Add(search); Page.Children.Add(list);
        Page.Children.Add(Action("Manage Java instances", () => service.StartEngine()));
        Page.Children.Add(Action("Manage Bedrock instances", () => service.StartLevi()));
    }
    private void Settings()
    {
        Heading("A launcher that fits you", "Your launchers are included. Choose where your worlds and instances live.");
        var engine = new TextBox { Text = service.Settings.EnginePath, Watermark = "Path to FLauncher.exe or prismlauncher.exe" };
        var data = new TextBox { Text = service.Settings.DataPath };
        var levi = new TextBox { Text = service.Settings.LeviPath, Watermark = "Optional path to LeviLauncher.exe" };
        Button Browse(TextBox field, bool folder)
        {
            var button = Action("Browse…", () => { });
            button.Click += async (_, _) =>
            {
                try
                {
                    if (folder) { var items = await StorageProvider.OpenFolderPickerAsync(new() { Title = "Select Prism data folder", AllowMultiple = false }); if (items.Count > 0) field.Text = items[0].TryGetLocalPath(); }
                    else { var items = await StorageProvider.OpenFilePickerAsync(new() { Title = "Select launcher executable", AllowMultiple = false }); if (items.Count > 0) field.Text = items[0].TryGetLocalPath(); }
                }
                catch (Exception ex) { await Message("File picker", ex.Message); }
            };
            return button;
        }
        Page.Children.Add(Card(Stack(Text("Included capabilities", 20),
            Text(service.HasBundledJava ? "✓  Java · Prism engine, accounts, modpacks and instance management" : "Java backend is missing from this build. Publish the complete distribution.", 14, !service.HasBundledJava),
            Text(service.HasBundledBedrock ? "✓  Bedrock · LeviLauncher installation, versions and mod management" : "Bedrock backend is missing from this build. Publish the complete distribution.", 14, !service.HasBundledBedrock))));
        Page.Children.Add(Card(Stack(Text("Game data folder", 18), data, Browse(data, true), Text("Java instances and a separate Bedrock profile are stored here. Existing files stay in their current location when you change this setting.", 13, true), Action("Save settings", () =>
        {
            if (string.IsNullOrWhiteSpace(data.Text) || !Path.IsPathFullyQualified(data.Text)) throw new IOException("Choose an absolute data folder path.");
            if (!service.HasBundledJava && !string.IsNullOrWhiteSpace(engine.Text) && !File.Exists(engine.Text)) throw new FileNotFoundException("The Java engine executable does not exist.");
            if (!service.HasBundledBedrock && !string.IsNullOrWhiteSpace(levi.Text) && !File.Exists(levi.Text)) throw new FileNotFoundException("The LeviLauncher executable does not exist.");
            service.Settings.EnginePath = engine.Text?.Trim() ?? ""; service.Settings.DataPath = data.Text.Trim(); service.Settings.LeviPath = levi.Text?.Trim() ?? ""; service.Save(); Status.Text = "Settings saved.";
        }, true))));
        if (!service.HasBundledJava || !service.HasBundledBedrock)
            Page.Children.Add(new Expander { Header = "Development build: external backend paths", Content = Stack(Text("Java engine"), engine, Browse(engine, false), Text("Bedrock engine"), levi, Browse(levi, false)) });
    }
    private void Java()
    {
        Heading("Java, without the guesswork", "Download a verified runtime, then assign it to the instance that needs it.");
        var versions = new ComboBox { ItemsSource = new[] { "Java 21", "Java 17", "Java 8", "Java 25" }, SelectedIndex = 0, MinWidth = 220 };
        var state = Text(installation is null ? "Ready to install Eclipse Temurin." : "An installation is running. Progress appears below.", 13, true);
        var install = Action("Download & install", () => { }, true);
        install.IsEnabled = installation is null;
        var progress = new ProgressBar { IsIndeterminate = true, IsVisible = installation is not null, Height = 4 };
        install.Click += async (_, _) =>
        {
            if (installation is not null) return;
            installation = new(); install.IsEnabled = false; progress.IsVisible = true;
            try
            {
                var major = new[] { 21, 17, 8, 25 }[versions.SelectedIndex];
                var path = await new JavaInstaller(http).InstallAsync(major, Path.Combine(service.Settings.DataPath, "java"), new Progress<string>(s => { state.Text = s; Status.Text = s; }), installation.Token);
                service.Settings.JavaPath = path; service.Save(); Status.Text = "Java installed. Select an instance below to apply it.";
            }
            catch (OperationCanceledException) { Status.Text = "Java installation cancelled. Existing runtimes were preserved."; }
            catch (Exception ex) { Status.Text = ex.Message; await Message("Java installation failed", ex.Message); }
            finally { installation.Dispose(); installation = null; if (currentPage == "Java") Render(); }
        };
        var cancel = Action("Cancel download", () => installation?.Cancel());
        cancel.IsEnabled = installation is not null;
        install.Click += (_, _) => cancel.IsEnabled = installation is not null;
        Page.Children.Add(Card(Stack(Text("Managed runtimes", 20), Text("Java 8 for older releases · Java 17 for 1.18–1.20.4 · Java 21 for 1.20.5+.\nCheck the requirements of newer releases before choosing Java 25.", 13, true), versions, install, cancel, progress, state)));
        var instances = service.GetInstances();
        var target = new ComboBox { ItemsSource = instances.Select(i => i.Name).ToArray(), SelectedIndex = instances.Count > 0 ? 0 : -1, HorizontalAlignment = HorizontalAlignment.Stretch };
        var apply = Action("Use this runtime for selected instance", () => { JavaInstaller.ApplyToInstance(instances[target.SelectedIndex], service.Settings.JavaPath); Status.Text = "Runtime assigned. Previous config saved as instance.cfg.before-java."; }, true);
        apply.IsEnabled = instances.Count > 0 && File.Exists(service.Settings.JavaPath);
        Page.Children.Add(Card(Stack(Text("Assign your runtime", 20), Text(string.IsNullOrEmpty(service.Settings.JavaPath) ? "Install a runtime to get started." : service.Settings.JavaPath, 12, true), target, Text("Close the Prism instance manager before applying so it cannot overwrite the setting.", 12, true), apply)));
    }
    private void Credits()
    {
        Heading("Built together. With gratitude.", "The people and projects that make FLauncher possible.");
        Page.Children.Add(Card(Stack(Text("Prism Launcher", 22), Text("Java launch engine, instance management, accounts, and modding infrastructure.", 14, true), Action("Visit Prism Launcher ↗", () => LauncherService.Open("https://prismlauncher.org")))));
        Page.Children.Add(Card(Stack(Text("LeviLauncher · LiteDev (LiteLDev)", 22), Text("Included Bedrock version / mod manager and design inspiration.", 14, true), Action("Visit LeviLauncher ↗", () => LauncherService.Open("https://github.com/LiteLDev/LeviLauncher")))));
        Page.Children.Add(Card(Stack(Text("Avalonia · Eclipse Adoptium", 20), Text("Cross-platform interface and Eclipse Temurin Java runtimes.", 14, true))));
        Page.Children.Add(Text(Attribution + " FLauncher is distributed under GPL-3.0-only; upstream notices remain in LICENSE and COPYING.md.", 13, true));
        Page.Children.Add(Action("Show welcome message", () => _ = Welcome()));
    }
    private Task Welcome() => Message("Welcome to FLauncher", "Your next adventure starts here.\n\n" + Attribution);
    private async Task Message(string title, string body)
    {
        var dialog = new Window { Title = title, Width = 560, SizeToContent = SizeToContent.Height, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var close = new Button { Content = "Let's go", HorizontalAlignment = HorizontalAlignment.Right }; close.Classes.Add("primary"); close.Click += (_, _) => dialog.Close();
        dialog.Content = new Border { Padding = new Thickness(32), Child = Stack(Text(title, 26), Text(body, 14, true), close) };
        await dialog.ShowDialog(this);
    }
}
