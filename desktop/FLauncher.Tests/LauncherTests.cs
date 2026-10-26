using System.Security.Cryptography;
using FLauncher.Core;
using Xunit;

namespace FLauncher.Tests;

public sealed class LauncherTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "flauncher-test-" + Guid.NewGuid());
    public LauncherTests() => Directory.CreateDirectory(root);
    [Fact]
    public async Task CorruptedDownloadIsRejected()
    {
        var path = Path.Combine(root, "runtime.zip");
        await File.WriteAllTextAsync(path, "original");
        var hash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path)));
        await JavaInstaller.VerifyChecksumAsync(path, hash, default);
        await File.AppendAllTextAsync(path, "corruption");
        await Assert.ThrowsAsync<IOException>(() => JavaInstaller.VerifyChecksumAsync(path, hash, default));
    }
    [Fact]
    public void AssigningJavaPreservesUnrelatedSettingsAndBacksUpOriginal()
    {
        var config = "[General]\nname=World = home\nJavaPath=old\nOverrideJavaLocation=false\nMaxMemAlloc=8192\n[Other]\nhello=world\n";
        File.WriteAllText(Path.Combine(root, "instance.cfg"), config);
        var java = Path.Combine(root, "java.exe"); File.WriteAllText(java, "test fixture");
        JavaInstaller.ApplyToInstance(new("test", "Test", "1.21", root), java);
        Assert.Equal(config, File.ReadAllText(Path.Combine(root, "instance.cfg.before-java")));
        Assert.Equal("World = home", LauncherService.ReadIni(Path.Combine(root, "instance.cfg"), "name"));
        Assert.Equal("8192", LauncherService.ReadIni(Path.Combine(root, "instance.cfg"), "MaxMemAlloc"));
        Assert.Equal("true", LauncherService.ReadIni(Path.Combine(root, "instance.cfg"), "OverrideJavaLocation"));
        Assert.Contains("[General]\nAutomaticJava=false", File.ReadAllText(Path.Combine(root, "instance.cfg")).Replace("\r\n", "\n"));
    }
    [Fact]
    public void MissingJavaDoesNotModifyInstance()
    {
        File.WriteAllText(Path.Combine(root, "instance.cfg"), "name=keep");
        Assert.Throws<FileNotFoundException>(() => JavaInstaller.ApplyToInstance(new("test", "Test", "", root), Path.Combine(root, "missing.exe")));
        Assert.Equal("name=keep", File.ReadAllText(Path.Combine(root, "instance.cfg")));
    }
    [Fact]
    public void LibraryReadsPrismMetadataWithoutChangingIt()
    {
        var instance = Path.Combine(root, "instances", "world"); Directory.CreateDirectory(instance);
        File.WriteAllText(Path.Combine(instance, "instance.cfg"), "[General]\nname=My world");
        File.WriteAllText(Path.Combine(instance, "mmc-pack.json"), "{\"components\":[{\"uid\":\"net.minecraft\",\"version\":\"1.21.1\"}]}");
        var service = new LauncherService(new LauncherSettings { DataPath = root });
        var result = Assert.Single(service.GetInstances());
        Assert.Equal("My world", result.Name); Assert.Equal("world", result.Id); Assert.Equal("Minecraft 1.21.1", result.Version);
        Assert.Equal(2, Directory.GetFiles(instance).Length);
    }
    [Fact]
    public async Task FailedDownloadCleansStagingAndPreservesExistingJava()
    {
        var existing = Path.Combine(root, "existing-java"); Directory.CreateDirectory(existing);
        File.WriteAllText(Path.Combine(existing, "java.exe"), "preserve");
        using var http = new HttpClient(new FailingHandler());
        await Assert.ThrowsAsync<HttpRequestException>(() => new JavaInstaller(http).InstallAsync(21, root, new Progress<string>(), default));
        Assert.Empty(Directory.GetDirectories(root, ".install-*"));
        Assert.Equal("preserve", File.ReadAllText(Path.Combine(existing, "java.exe")));
    }
    [Fact]
    public async Task CancellationCleansStaging()
    {
        using var http = new HttpClient(new FailingHandler());
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new JavaInstaller(http).InstallAsync(21, root, new Progress<string>(), cancellation.Token));
        Assert.Empty(Directory.GetDirectories(root, ".install-*"));
    }
    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable));
        }
    }
    public void Dispose() => Directory.Delete(root, true);
}
