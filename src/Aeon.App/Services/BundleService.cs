using System.Text.Json;
using Aeon.Data;

namespace Aeon.App.Services;

/// <summary>
/// Loads the published bundle through the tested Aeon.Data layer. On first run the
/// shipped app-bundle.json (a copy of the frozen, verified build artifact) seeds the
/// cache, so a fresh install with no network is the FromCacheOffline state rather than
/// a failure. From then on BundleFetcher owns download, hash verification and caching,
/// exactly as it does for the CLI.
///
/// Fail-safe contract: a read-only browser that shows an empty list looks broken, so
/// there are two independent sources and a corrupt cache is never the last word.
///   1. the cache, which BundleFetcher writes only after both a hash check and a clean
///      parse, so a bad download can never displace a good one;
///   2. the bundle inside the APK, verified the same way a download would be.
/// If the fetcher returns nothing (no network AND an unusable cache), the embedded copy
/// is loaded instead, and the caller is told so via ServedFromEmbeddedFallback.
/// </summary>
public sealed class BundleService
{
    private const string EmbeddedAssetName = "app-bundle.json";

    private readonly BundleFetcher _fetcher;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public BundleService(BundleFetcher fetcher) => _fetcher = fetcher;

    public Bundle? Data { get; private set; }
    public BundleLoadStatus? Status { get; private set; }
    public string? Error { get; private set; }
    public bool IsLoaded => Data is not null;
    public string? Version => Data?.Version;

    /// <summary>
    /// True when the fetcher had nothing usable and the copy shipped in the APK is what
    /// is on screen. Reported in the footer rather than hidden, so a stale-looking app is
    /// never mistaken for a fresh one.
    /// </summary>
    public bool ServedFromEmbeddedFallback { get; private set; }

    public async Task LoadAsync()
    {
        if (Data is not null)
        {
            return;
        }

        await SeedCacheFromEmbeddedIfMissingAsync();

        var result = await _fetcher.LoadAsync();
        Status = result.Status;
        Error = result.Error;
        Data = result.Bundle;

        if (Data is null)
        {
            // Last resort. Reached only when the cache is unusable and there is no
            // network. Repairing the cache here as well means the app does not stay
            // degraded on every subsequent offline launch.
            var embedded = await TryLoadEmbeddedAsync();
            if (embedded is not null)
            {
                Data = embedded.Value.Bundle;
                ServedFromEmbeddedFallback = true;
                TryWriteCache(embedded.Value.Bytes, embedded.Value.Bundle.Version);
            }
        }
    }

    /// <summary>
    /// Only seeds when there is no cache at all. The embedded asset is itself a bundle,
    /// so it is verified the same way a download would be before it is trusted: parse,
    /// then recompute its version and compare to the version it advertises. A corrupt
    /// asset quietly no-ops and the fetcher falls through to the network path.
    /// </summary>
    private async Task SeedCacheFromEmbeddedIfMissingAsync()
    {
        if (_fetcher.HasCache)
        {
            return;
        }

        var embedded = await TryLoadEmbeddedAsync();
        if (embedded is not null)
        {
            TryWriteCache(embedded.Value.Bytes, embedded.Value.Bundle.Version);
        }
    }

    /// <summary>
    /// Reads the bundle packaged in the APK and verifies it exactly as a download would
    /// be: parse, then recompute the version and compare. A corrupt or truncated asset
    /// no-ops and the caller falls through.
    /// </summary>
    private async Task<(Bundle Bundle, byte[] Bytes)?> TryLoadEmbeddedAsync()
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync(EmbeddedAssetName);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            var bytes = buffer.ToArray();

            var bundle = JsonSerializer.Deserialize<Bundle>(bytes, _json);
            if (bundle is null || string.IsNullOrEmpty(bundle.Version))
            {
                return null;
            }
            if (!BundleHasher.MatchesServedVersion(bytes, bundle.Version))
            {
                return null;
            }

            return (bundle, bytes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Embedded asset unavailable on this platform: the fetcher's network path
            // is the only source, and it will surface its own failure state.
            return null;
        }
    }

    private void TryWriteCache(byte[] bundleBytes, string version)
    {
        try
        {
            Directory.CreateDirectory(_fetcher.CacheDirectory);
            File.WriteAllBytes(
                Path.Combine(_fetcher.CacheDirectory, "app-bundle.json"), bundleBytes);
            File.WriteAllBytes(
                Path.Combine(_fetcher.CacheDirectory, "bundle-meta.json"),
                JsonSerializer.SerializeToUtf8Bytes(new BundleMeta { Version = version }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The cache is an optimisation. Failing to write it costs a slower next
            // launch, not correctness, since the embedded asset is verified on read.
        }
    }
}