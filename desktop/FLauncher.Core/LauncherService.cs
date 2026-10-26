// SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;
using System.Text.Json;

namespace FLauncher.Core;

public sealed class LauncherSettings
{
    public string EnginePath { get; set; } = "";
    public string DataPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FLauncher");
    public string LeviPath { get; set; } = "";
    public string JavaPath { get; set; } = "";
}

public sealed record GameInstance(string Id, string Name, string Version, string Directory);
public sealed record BedrockApp(string Name, string AppId);
public sealed record BedrockInstance(string Name, string Version, string Channel, string Directory);

public sealed class LauncherService
{
    public static string SettingsFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FLauncher", "desktop.json");
    public LauncherSettings Settings { get; }
    private readonly string bundleRoot;
    public string BundledJavaPath => new[] { "FLauncher.exe", "prismlauncher.exe" }
        .Select(name => Path.Combine(bundleRoot, "backends", "java", name)).FirstOrDefault(File.Exists) ?? "";
    public string BundledBedrockPath => Path.Combine(bundleRoot, "backends", "bedrock", "LeviLauncher.exe");
    public bool HasBundledJava => File.Exists(BundledJavaPath);
    public bool HasBundledBedrock => File.Exists(BundledBedrockPath);
    public string EnginePath => HasBundledJava ? BundledJavaPath : Settings.EnginePath;
    public string BedrockPath => HasBundledBedrock ? BundledBedrockPath : Settings.LeviPath;
    public string BedrockProfilePath => Path.Combine(Settings.DataPath, "bedrock", "profile");
    public string BedrockConfigRoot => Path.Combine(HasBundledBedrock ? BedrockProfilePath : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Path.GetFileName(BedrockPath).ToLowerInvariant());
    public LauncherService(LauncherSettings? settings = null, string? bundleRoot = null)
    {
        this.bundleRoot = bundleRoot ?? AppContext.BaseDirectory;
        Settings = settings ?? (File.Exists(SettingsFile)
            ? JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(SettingsFile)) ?? new()
            : new());
        if (string.IsNullOrEmpty(Settings.EnginePath))
        {
            var names = OperatingSystem.IsWindows() ? new[] { "FLauncher.exe", "prismlauncher.exe" } : new[] { "FLauncher", "prismlauncher" };
            Settings.EnginePath = names.Select(name => Path.Combine(AppContext.BaseDirectory, "engine", name)).FirstOrDefault(File.Exists) ?? "";
        }
    }
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
        File.WriteAllText(SettingsFile + ".tmp", JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(SettingsFile + ".tmp", SettingsFile, true);
    }
    public IReadOnlyList<GameInstance> GetInstances()
    {
        var root = Path.Combine(Settings.DataPath, "instances");
        if (!System.IO.Directory.Exists(root)) return [];
        return System.IO.Directory.EnumerateDirectories(root).Where(d => File.Exists(Path.Combine(d, "instance.cfg"))).Select(d =>
        {
            var name = ReadIni(Path.Combine(d, "instance.cfg"), "name") ?? Path.GetFileName(d);
            var version = "Minecraft Java";
            try
            {
                using var pack = JsonDocument.Parse(File.ReadAllText(Path.Combine(d, "mmc-pack.json")));
                version = string.Join(" · ", pack.RootElement.GetProperty("components").EnumerateArray()
                    .Where(c => c.TryGetProperty("version", out _))
                    .Select(c => $"{c.GetProperty("uid").GetString()?.Replace("net.minecraft", "Minecraft")} {c.GetProperty("version").GetString()}"));
            }
            catch (Exception e) when (e is IOException or JsonException or KeyNotFoundException) { }
            return new GameInstance(Path.GetFileName(d), name, version, d);
        }).OrderBy(i => i.Name).ToArray();
    }
    public static string? ReadIni(string path, string key) => File.ReadLines(path)
        .Select(l => l.Split('=', 2)).FirstOrDefault(p => p.Length == 2 && p[0].Trim() == key)?[1].Trim();

    public void StartEngine(string? instance = null)
    {
        Process.Start(CreateEngineStartInfo(instance));
    }
    public ProcessStartInfo CreateEngineStartInfo(string? instance = null)
    {
        if (string.IsNullOrWhiteSpace(EnginePath) || !File.Exists(EnginePath)) throw new FileNotFoundException("The Java backend is missing. Extract the complete FLauncher build again, including its backends folder.");
        var engine = Path.GetFullPath(EnginePath);
        if (string.Equals(engine, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Select the Prism engine, not the Avalonia front end.");
        Directory.CreateDirectory(Settings.DataPath);
        var start = new ProcessStartInfo(engine) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(engine)! };
        start.ArgumentList.Add("--dir"); start.ArgumentList.Add(Path.GetFullPath(Settings.DataPath));
        if (instance is not null) { start.ArgumentList.Add("--launch"); start.ArgumentList.Add(instance); }
        return start;
    }
    public void StartLevi(string? instance = null) => Process.Start(CreateBedrockStartInfo(instance));
    public ProcessStartInfo CreateBedrockStartInfo(string? instance = null)
    {
        if (!File.Exists(BedrockPath)) throw new FileNotFoundException("The Bedrock backend is missing. Extract the complete FLauncher build again, including its backends folder.");
        var start = new ProcessStartInfo(Path.GetFullPath(BedrockPath)) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(BedrockPath))! };
        if (HasBundledBedrock)
        {
            Directory.CreateDirectory(BedrockProfilePath);
            start.Environment["APPDATA"] = Path.GetFullPath(BedrockProfilePath);
        }
        if (!string.IsNullOrWhiteSpace(instance)) start.ArgumentList.Add("--launch=" + instance);
        return start;
    }
    public IReadOnlyList<BedrockInstance> GetBedrockInstances()
    {
        if (string.IsNullOrWhiteSpace(BedrockPath)) return [];
        var root = BedrockConfigRoot;
        var configFile = Path.Combine(root, "config.json");
        if (File.Exists(configFile))
        {
            using var config = JsonDocument.Parse(File.ReadAllText(configFile));
            if (config.RootElement.TryGetProperty("base_root", out var configured) && !string.IsNullOrWhiteSpace(configured.GetString())) root = configured.GetString()!;
        }
        var versions = Path.Combine(root, "versions");
        if (!Directory.Exists(versions)) return [];
        var result = new List<BedrockInstance>();
        foreach (var folder in Directory.EnumerateDirectories(versions))
        {
            var meta = Path.Combine(folder, "version.json");
            if (!File.Exists(meta)) continue;
            using var doc = JsonDocument.Parse(File.ReadAllText(meta));
            var data = doc.RootElement;
            result.Add(new BedrockInstance(data.GetProperty("name").GetString() ?? Path.GetFileName(folder),
                data.GetProperty("gameVersion").GetString() ?? "", data.GetProperty("type").GetString() ?? "Release", folder));
        }
        return result.OrderBy(i => i.Name).ToArray();
    }
    public static void Open(string target) => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
    public static async Task<IReadOnlyList<BedrockApp>> FindBedrockAsync()
    {
        if (!OperatingSystem.IsWindows()) return [];
        // Fixed script: paths and user-controlled text never enter PowerShell source.
        const string script = "Get-StartApps | Where-Object { $_.AppID -match '^(Microsoft\\.Minecraft(UWP|WindowsBeta)|Microsoft\\.Minecraft)[^!]*!' } | Select-Object Name,AppID | ConvertTo-Json -Compress";
        var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-Command", script }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new IOException("Could not query Windows apps.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Windows app discovery timed out. Try refreshing."); }
        if (process.ExitCode != 0) throw new IOException(await error);
        var json = await output;
        if (string.IsNullOrWhiteSpace(json)) return [];
        using var doc = JsonDocument.Parse(json);
        var entries = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.EnumerateArray().ToArray() : [doc.RootElement];
        return entries.Select(e => new BedrockApp(e.GetProperty("Name").GetString()!, e.GetProperty("AppID").GetString()!)).ToArray();
    }
    public static void LaunchBedrock(BedrockApp app)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Bedrock launch requires Windows.");
        var start = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
        start.ArgumentList.Add("shell:AppsFolder\\" + app.AppId);
        Process.Start(start);
    }
}
