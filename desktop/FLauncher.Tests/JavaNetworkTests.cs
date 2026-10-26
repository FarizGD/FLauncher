using FLauncher.Core;
using Xunit;

namespace FLauncher.Tests;
public sealed class JavaNetworkTests
{
    // Opt-in smoke check: use network.runsettings as documented in desktop/README.md.
    [Fact, Trait("Category", "Network")]
    public async Task Temurin21DownloadsVerifiesAndRuns()
    {
        var root = Path.Combine(Path.GetTempPath(), "flauncher-java-smoke-" + Guid.NewGuid());
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        try
        {
            var java = await new JavaInstaller(http).InstallAsync(21, root, new Progress<string>(), default);
            Assert.True(File.Exists(java));
            Assert.Empty(Directory.GetDirectories(root, ".install-*"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
