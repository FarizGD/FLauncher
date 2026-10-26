using System.Text.Json;
using FLauncher.Core;
using Xunit;

namespace FLauncher.Tests;
public sealed class BundleTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "flauncher bundle " + Guid.NewGuid());
    public BundleTests()
    {
        Directory.CreateDirectory(Path.Combine(root, "backends", "java"));
        Directory.CreateDirectory(Path.Combine(root, "backends", "bedrock"));
        File.WriteAllText(Path.Combine(root, "backends", "java", "prismlauncher.exe"), "fixture");
        File.WriteAllText(Path.Combine(root, "backends", "bedrock", "LeviLauncher.exe"), "fixture");
    }
    private LauncherService Service() => new(new LauncherSettings { DataPath = Path.Combine(root, "my data"), EnginePath = "X:/old/install/prismlauncher.exe", LeviPath = "X:/old/install/LeviLauncher.exe" }, root);
    [Fact]
    public void BundledEnginesOverrideStaleExternalSettings()
    {
        var service = Service();
        Assert.True(service.HasBundledJava); Assert.True(service.HasBundledBedrock);
        Assert.StartsWith(root, service.EnginePath); Assert.StartsWith(root, service.BedrockPath);
    }
    [Fact]
    public void JavaLaunchKeepsPathsAndInstanceNameAsSeparateArguments()
    {
        var service = Service();
        var process = service.CreateEngineStartInfo("My modpack --offline");
        Assert.Equal(["--dir", Path.GetFullPath(service.Settings.DataPath), "--launch", "My modpack --offline"], process.ArgumentList);
        Assert.False(process.UseShellExecute);
        Assert.Equal(service.EnginePath, process.FileName);
    }
    [Fact]
    public void BedrockLaunchUsesIsolatedProfileAndSingleLaunchArgument()
    {
        var service = Service(); var process = service.CreateBedrockStartInfo("My Preview");
        Assert.Equal(["--launch=My Preview"], process.ArgumentList);
        Assert.Equal(Path.GetFullPath(service.BedrockProfilePath), process.Environment["APPDATA"]);
        Assert.False(process.UseShellExecute);
    }
    [Fact]
    public void BedrockLibraryFollowsBackendDataDirectory()
    {
        var service = Service();
        Directory.CreateDirectory(service.BedrockConfigRoot);
        var data = Path.Combine(root, "custom bedrock");
        File.WriteAllText(Path.Combine(service.BedrockConfigRoot, "config.json"), JsonSerializer.Serialize(new { base_root = data }));
        var version = Path.Combine(data, "versions", "my preview"); Directory.CreateDirectory(version);
        File.WriteAllText(Path.Combine(version, "version.json"), "{\"name\":\"My Preview\",\"gameVersion\":\"1.21.0\",\"type\":\"Preview\"}");
        var instance = Assert.Single(service.GetBedrockInstances());
        Assert.Equal("My Preview", instance.Name); Assert.Equal("1.21.0", instance.Version); Assert.Equal(version, instance.Directory);
    }
    [Fact]
    public void RelocatingTheBundleDoesNotBreakBackendResolution()
    {
        var moved = Path.Combine(root, "relocated app"); Directory.CreateDirectory(moved);
        Directory.Move(Path.Combine(root, "backends"), Path.Combine(moved, "backends"));
        var service = new LauncherService(new LauncherSettings(), moved);
        Assert.True(service.HasBundledJava); Assert.True(service.HasBundledBedrock);
    }
    public void Dispose() => Directory.Delete(root, true);
}
