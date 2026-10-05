using System.Net.Http.Headers;
using System.Text.Json;

namespace Aeon.Data;

public enum BundleLoadStatus
{
    /// <summary>Remote version differed from cache; bundle downloaded and verified.</summary>
    Downloaded,

    /// <summary>Remote version matched the cached version; served from cache, no download.</summary>
    CacheCurrent,

    /// <summary>Network unavailable; fell back to cache. Data may be stale.</summary>
    FromCacheOffline,

    /// <summary>Downloaded, but the bytes did not hash to the advertised version.</summary>
    IntegrityFailed,

    /// <summary>No network and no usable cache. Nothing to show. This is a real error.</summary>
    Failed,
}

public sealed record BundleLoadResult
{
    public required BundleLoadStatus Status { get; init; }
    public Bundle? Bundle { get; init; }
    public string? RemoteVersion { get; init; }
    public string? CachedVersion { get; init; }
    public string? Error { get; init; }
    public string CacheDirectory { get; init; } = "";

    /// <summary>Build time of the data actually returned. Never null unless Failed.</summary>
    public DateTimeOffset? GeneratedAt => Bundle?.GeneratedAt;

    /// <summary>How long ago the returned data was built. Null unless we have data.</summary>
    public TimeSpan? Age => GeneratedAt is null ? null : DateTimeOffset.UtcNow - GeneratedAt.Value;

    public bool HasData => Bundle is not null;
}

/// <summary>
/// Fetches the published bundle from GitHub, with a local cache and a real integrity
/// check. No auth: the repo is public and raw.githubusercontent.com serves the file
/// directly.
///
/// Cache lives under Environment.SpecialFolder.LocalApplicationData, which is the
/// correct per-user location on both Windows (%LOCALAPPDATA%) and Android once the
/// app is packaged. Deliberately NOT a temp dir or a project-relative path: temp gets
/// cleared unpredictably, and relative paths break once packaged into an app sandbox.
/// </summary>
public sealed class BundleFetcher
{
    public const string BaseUrl =
        "https://raw.githubusercontent.com/Nirav846/AEON/main/build";

    private readonly HttpClient _http;
    private readonly string _cacheDir;
    private readonly string _baseUrl;
    private readonly JsonSerializerOptions _json;

    public BundleFetcher(HttpClient? http = null, string? cacheDirectory = null,
                         string? baseUrl = null)
    {
        _http = http ?? CreateDefaultClient();
        _cacheDir = cacheDirectory ?? DefaultCacheDirectory();
        _baseUrl = (baseUrl ?? BaseUrl).TrimEnd('/');
        _json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    public string BaseUrlOverride => _baseUrl;

    public static string DefaultCacheDirectory()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aeon", "cache");

    public string CacheDirectory => _cacheDir;

    private string MetaPath => Path.Combine(_cacheDir, "bundle-meta.json");
    private string BundlePath => Path.Combine(_cacheDir, "app-bundle.json");

    private static HttpClient CreateDefaultClient()
    {
        var client = new HttpClient
        {
            // Short enough that a dead gym wifi fails fast into the offline path,
            // long enough for a slow-but-working connection.
            Timeout = TimeSpan.FromSeconds(20),
        };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("Aeon.Data", "1.0"));
        return client;
    }

    /// <summary>
    /// Fetch meta, compare against cache, download only on a version change, verify
    /// the downloaded bytes against the advertised version, and cache on success.
    /// Never throws for expected failures - returns a Failed result instead, because
    /// "no network on first launch" is a normal state, not an exception.
    /// </summary>
    public async Task<BundleLoadResult> LoadAsync(CancellationToken ct = default)
    {
        var cachedVersion = ReadCachedMetaVersion();

        // --- cheap poll first: 203 bytes decides whether we download 126 KB ---
        BundleMeta? remote;
        try
        {
            var metaBytes = await _http.GetByteArrayAsync($"{_baseUrl}/bundle-meta.json", ct);
            remote = JsonSerializer.Deserialize<BundleMeta>(metaBytes, _json);
            if (remote is null || string.IsNullOrEmpty(remote.Version))
            {
                return FallBack(cachedVersion, null,
                    "bundle-meta.json parsed but carried no version field");
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                      or JsonException or InvalidOperationException)
        {
            // No network, or the CDN served something unparseable.
            return FallBack(cachedVersion, null, $"{ex.GetType().Name}: {ex.Message}");
        }

        // --- cache is already current: no download at all ---
        if (cachedVersion is not null && cachedVersion == remote!.Version && File.Exists(BundlePath))
        {
            var cached = TryReadBundle(BundlePath);
            if (cached is not null)
            {
                return new BundleLoadResult
                {
                    Status = BundleLoadStatus.CacheCurrent,
                    Bundle = cached,
                    RemoteVersion = remote.Version,
                    CachedVersion = cachedVersion,
                    CacheDirectory = _cacheDir,
                };
            }
        }

        // --- version differs (or we have no cache): download and verify ---
        byte[] bytes;
        try
        {
            bytes = await _http.GetByteArrayAsync($"{_baseUrl}/app-bundle.json", ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                      or InvalidOperationException)
        {
            return FallBack(cachedVersion, remote.Version, $"{ex.GetType().Name}: {ex.Message}");
        }

        var computed = BundleHasher.ComputeVersion(bytes);
        if (computed != remote!.Version)
        {
            // Integrity failure. Expected in practice during the 1-2 min CDN
            // propagation window: meta.json is a separate file from app-bundle.json
            // and can be cached independently, so we can briefly see a FRESH meta
            // next to a STALE bundle. The check exists precisely to catch that -
            // without it we would cache a mismatched pair and serve it indefinitely.
            return FallBack(cachedVersion, remote.Version,
                $"integrity check failed: bundle hashes to {computed} but meta advertises " +
                $"{remote.Version} (likely CDN propagation lag - retry shortly)");
        }

        var parsed = TryDeserializeBundle(bytes);
        if (parsed is null)
        {
            return FallBack(cachedVersion, remote.Version,
                "bundle verified against its version but failed to deserialize");
        }

        // Only cache once the bytes are verified, so a bad download can never
        // overwrite a good cache.
        WriteCache(bytes, metaBytesOf(remote));

        return new BundleLoadResult
        {
            Status = BundleLoadStatus.Downloaded,
            Bundle = parsed,
            RemoteVersion = remote.Version,
            CachedVersion = cachedVersion,
            CacheDirectory = _cacheDir,
        };
    }

    private static byte[] metaBytesOf(BundleMeta meta)
        => JsonSerializer.SerializeToUtf8Bytes(meta);

    private BundleLoadResult FallBack(string? cachedVersion, string? remoteVersion, string error)
    {
        if (File.Exists(BundlePath))
        {
            var cached = TryReadBundle(BundlePath);
            if (cached is not null)
            {
                return new BundleLoadResult
                {
                    Status = BundleLoadStatus.FromCacheOffline,
                    Bundle = cached,
                    RemoteVersion = remoteVersion,
                    CachedVersion = cachedVersion ?? cached.Version,
                    Error = error,
                    CacheDirectory = _cacheDir,
                };
            }
        }

        return new BundleLoadResult
        {
            Status = BundleLoadStatus.Failed,
            RemoteVersion = remoteVersion,
            CachedVersion = cachedVersion,
            Error = $"{error}. No usable cache at {BundlePath} - cannot continue.",
            CacheDirectory = _cacheDir,
        };
    }

    private Bundle? TryReadBundle(string path)
    {
        try
        {
            return TryDeserializeBundle(File.ReadAllBytes(path));
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    private Bundle? TryDeserializeBundle(byte[] bytes)
    {
        try
        {
            return JsonSerializer.Deserialize<Bundle>(bytes, _json);
        }
        catch (JsonException) { return null; }
    }

    private string? ReadCachedMetaVersion()
    {
        if (!File.Exists(MetaPath)) return null;
        try
        {
            var meta = JsonSerializer.Deserialize<BundleMeta>(
                File.ReadAllBytes(MetaPath), _json);
            return string.IsNullOrEmpty(meta?.Version) ? null : meta.Version;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;   // a corrupt cache is a cache miss, not a fatal error
        }
    }

    private void WriteCache(byte[] bundleBytes, byte[] metaBytes)
    {
        Directory.CreateDirectory(_cacheDir);
        WriteAtomic(BundlePath, bundleBytes);
        WriteAtomic(MetaPath, metaBytes);
    }

    /// <summary>
    /// Write to a temp file then move into place, so a crash or a full disk mid-write
    /// cannot leave a truncated app-bundle.json that would fail to parse on next launch.
    /// </summary>
    private static void WriteAtomic(string path, byte[] bytes)
    {
        var tmp = path + ".tmp";
        File.WriteAllBytes(tmp, bytes);
        File.Move(tmp, path, overwrite: true);
    }

    /// <summary>True when a cache file exists, for the CLI to show first-run behaviour.</summary>
    public bool HasCache => File.Exists(BundlePath);
}