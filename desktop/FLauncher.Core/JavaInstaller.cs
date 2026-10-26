// SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace FLauncher.Core;

public sealed class JavaInstaller(HttpClient http)
{
    public async Task<string> InstallAsync(int major, string root, IProgress<string> progress, CancellationToken cancellation)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The managed installer currently supports Windows. Use Prism's runtime manager on other systems.");
        if (major is not (8 or 17 or 21 or 25)) throw new ArgumentOutOfRangeException(nameof(major));
        var arch = RuntimeInformation.OSArchitecture switch { Architecture.X64 => "x64", Architecture.Arm64 => "aarch64", _ => throw new PlatformNotSupportedException("A 64-bit Windows installation is required.") };
        Directory.CreateDirectory(root);
        var staging = Path.Combine(root, ".install-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            progress.Report("Finding Eclipse Temurin Java " + major + "…");
            using var metadata = await http.GetFromJsonAsync<JsonDocument>($"https://api.adoptium.net/v3/assets/latest/{major}/hotspot?architecture={arch}&image_type=jre&os=windows&vendor=eclipse", cancellation)
                ?? throw new IOException("The Java provider returned no metadata.");
            var entries = metadata.RootElement.EnumerateArray().ToArray();
            if (entries.Length == 0) throw new IOException("No matching runtime is available for this architecture.");
            var package = entries[0].GetProperty("binary").GetProperty("package");
            var url = new Uri(package.GetProperty("link").GetString()!);
            if (url.Scheme != "https") throw new IOException("The runtime download must use HTTPS.");
            var expected = package.GetProperty("checksum").GetString()!;
            var zip = Path.Combine(staging, "runtime.zip");
            using (var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellation))
            {
                response.EnsureSuccessStatusCode();
                await using var source = await response.Content.ReadAsStreamAsync(cancellation);
                await using var destination = File.Create(zip);
                var buffer = new byte[81920]; long total = 0; int count;
                while ((count = await source.ReadAsync(buffer, cancellation)) > 0)
                {
                    await destination.WriteAsync(buffer.AsMemory(0, count), cancellation); total += count;
                    progress.Report($"Downloading Java {major} · {total / 1048576} MB");
                }
            }
            progress.Report("Verifying SHA-256 checksum…");
            await VerifyChecksumAsync(zip, expected, cancellation);
            progress.Report("Extracting runtime…");
            var unpacked = Path.Combine(staging, "unpacked");
            await Task.Run(() => ZipFile.ExtractToDirectory(zip, unpacked), cancellation);
            cancellation.ThrowIfCancellationRequested();
            var java = Directory.GetFiles(unpacked, "java.exe", SearchOption.AllDirectories).SingleOrDefault(p => Path.GetFileName(Path.GetDirectoryName(p)) == "bin")
                ?? throw new IOException("The archive does not contain a Java executable.");
            var start = new ProcessStartInfo(java) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
            start.ArgumentList.Add("-version");
            using var process = Process.Start(start) ?? throw new IOException("Could not validate Java.");
            var stderr = process.StandardError.ReadToEndAsync(cancellation);
            var stdout = process.StandardOutput.ReadToEndAsync(cancellation);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch { if (!process.HasExited) process.Kill(true); throw; }
            var version = await stderr + await stdout;
            if (process.ExitCode != 0 || !version.Contains(major == 8 ? "\"1.8." : $"\"{major}.")) throw new IOException("The downloaded runtime failed its version check.");
            cancellation.ThrowIfCancellationRequested();
            // Each install has a unique destination; a failed update never removes a working runtime.
            var final = Path.Combine(root, $"temurin-{major}-{Guid.NewGuid():N}");
            var relative = Path.GetRelativePath(unpacked, java);
            Directory.Move(unpacked, final);
            progress.Report($"Java {major} installed and verified.");
            return Path.Combine(final, relative);
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
    }
    public static async Task VerifyChecksumAsync(string file, string expected, CancellationToken cancellation)
    {
        await using var input = File.OpenRead(file);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(input, cancellation));
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new IOException("Java checksum mismatch. The download was rejected; retry the installation.");
    }
    public static void ApplyToInstance(GameInstance instance, string java)
    {
        if (!File.Exists(java)) throw new FileNotFoundException("Install a runtime first.");
        var file = Path.Combine(instance.Directory, "instance.cfg");
        var lines = File.ReadAllLines(file).ToList();
        Set("OverrideJavaLocation", "true"); Set("JavaPath", java.Replace('\\', '/')); Set("AutomaticJava", "false");
        File.Copy(file, file + ".before-java", true);
        File.WriteAllLines(file + ".tmp", lines);
        File.Move(file + ".tmp", file, true);
        void Set(string key, string value)
        {
            var index = lines.FindIndex(l => l.StartsWith(key + "=", StringComparison.Ordinal));
            if (index >= 0) lines[index] = key + "=" + value;
            else
            {
                var general = lines.FindIndex(l => l.Trim() == "[General]");
                if (general < 0) { lines.Insert(0, "[General]"); general = 0; }
                lines.Insert(general + 1, key + "=" + value);
            }
        }
    }
}
