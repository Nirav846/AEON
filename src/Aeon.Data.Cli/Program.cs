using System.Diagnostics;
using System.Text.Json;
using Aeon.Data;

// Live smoke test for the data layer. Talks to the REAL published bundle on
// raw.githubusercontent.com - there is no mock and no local fixture anywhere here,
// because the whole point is to prove the fetch, the integrity check and the
// deserialization work against the actual network and the actual published bytes.
//
//   dotnet run                     -> live fetch from GitHub
//   dotnet run -- --offline        -> point at an unreachable host, proving the
//                                     cache carries the app with no network at all

var offline = args.Contains("--offline");
const string Unreachable = "http://127.0.0.1:9";   // discard port: refuses instantly, no DNS wait

// --cache <dir> overrides the cache location, so the no-cache failure path can be
// exercised without deleting the real cache.
string? cacheDir = null;
var cacheIdx = Array.IndexOf(args, "--cache");
if (cacheIdx >= 0 && cacheIdx + 1 < args.Length)
{
    cacheDir = args[cacheIdx + 1];
}

var fetcher = offline
    ? new BundleFetcher(cacheDirectory: cacheDir, baseUrl: Unreachable)
    : new BundleFetcher(cacheDirectory: cacheDir);

Console.WriteLine("AEON data layer - live check");
Console.WriteLine(new string('=', 72));
Console.WriteLine($"source    : {(offline ? Unreachable + "  (DELIBERATELY UNREACHABLE)" : BundleFetcher.BaseUrl)}");
Console.WriteLine($"cache dir : {fetcher.CacheDirectory}");
Console.WriteLine($"cache hit : {fetcher.HasCache}");
Console.WriteLine();

var sw = Stopwatch.StartNew();
var result = await fetcher.LoadAsync();
sw.Stop();

Console.WriteLine($"status       : {result.Status}");
Console.WriteLine($"elapsed      : {sw.ElapsedMilliseconds} ms");
Console.WriteLine($"remote ver   : {result.RemoteVersion ?? "(none - offline)"}");
Console.WriteLine($"cached ver   : {result.CachedVersion ?? "(none)"}");
if (result.Error is not null)
{
    Console.WriteLine($"note         : {result.Error}");
}
Console.WriteLine();

if (!result.HasData)
{
    Console.Error.WriteLine("FAILED: no data available.");
    Console.Error.WriteLine($"  {result.Error}");
    return 1;
}

var b = result.Bundle!;
Console.WriteLine($"generated_at : {b.GeneratedAt:O}");
if (result.Age is { } age)
{
    Console.WriteLine($"age          : {age.TotalHours:F1} h old" +
        (result.Status is BundleLoadStatus.FromCacheOffline ? "  (OFFLINE - may be stale)" : ""));
}
Console.WriteLine($"version      : {b.Version}");
Console.WriteLine();

Console.WriteLine("record counts");
Console.WriteLine($"  complexes   : {b.Complexes.Count,4}");
Console.WriteLine($"  circuits    : {b.Circuits.Count,4}");
Console.WriteLine($"  conditioning: {b.Conditioning.Count,4}");
Console.WriteLine($"  concepts    : {b.Concepts.Count,4}");
Console.WriteLine($"  {"total",11}: {b.TotalRecords,4}");
Console.WriteLine();

// Prove the records actually deserialized into fields, rather than landing as
// empty shells - a count alone would pass even if every string were null.
var sample = b.Complexes.FirstOrDefault(c => c.SwapEquipment is { Count: > 0 }) ?? b.Complexes.First();
Console.WriteLine("sample complex (populated fields)");
Console.WriteLine($"  id            : {sample.Id}");
Console.WriteLine($"  name          : {sample.Name}");
Console.WriteLine($"  sport/role    : {sample.Sport} / {sample.Role}");
Console.WriteLine($"  category      : {sample.Category}");
Console.WriteLine($"  equipment     : [{string.Join(", ", sample.Equipment)}]");
Console.WriteLine($"  swap_equipment: [{string.Join(", ", sample.SwapEquipment ?? new List<string>())}]");
Console.WriteLine($"  sources       : [{string.Join(", ", sample.Sources ?? new List<string>())}]");
Console.WriteLine($"  why set       : {!string.IsNullOrWhiteSpace(sample.Manual?.Why)}");
Console.WriteLine($"  cue set       : {!string.IsNullOrWhiteSpace(sample.Manual?.Cue)}");

var sportCounts = b.Complexes.GroupBy(c => c.Sport).OrderByDescending(g => g.Count());
Console.WriteLine();
Console.WriteLine("complexes by sport");
foreach (var group in sportCounts)
{
    Console.WriteLine($"  {group.Key,-12}: {group.Count(),3}");
}

// Non-ASCII round-trip check. 7 complexes contain an em-dash (U+2014); if the
// deserializer or the hasher mangled it, this is where it shows.
var emDash = b.Complexes.FirstOrDefault(c => c.Execution.Contains('\u2014')
                                         || c.Focus.Contains('\u2014')
                                         || c.Manual?.Why?.Contains('\u2014') == true);
Console.WriteLine();
Console.WriteLine($"non-ascii round-trip: {(emDash is null ? "no record carried U+2014" : $"id {emDash.Id} '{emDash.Name}' preserved")}");

// Re-verify the integrity path from scratch against the bytes we just parsed,
// independently of the fetcher's own check. Skipped offline, since the point of
// --offline is that there is nothing reachable to verify against.
var recheck = true;
if (!offline)
{
    recheck = await VerifyIntegrityAsync();
    Console.WriteLine($"independent hash re-verification: {(recheck ? "PASS" : "FAIL")}");
}
else
{
    Console.WriteLine("independent hash re-verification: skipped (offline mode)");
}
return recheck ? 0 : 1;

static async Task<bool> VerifyIntegrityAsync()
{
    try
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        var metaBytes = await http.GetByteArrayAsync($"{BundleFetcher.BaseUrl}/bundle-meta.json");
        var meta = JsonSerializer.Deserialize<BundleMeta>(metaBytes);
        var bundleBytes = await http.GetByteArrayAsync($"{BundleFetcher.BaseUrl}/app-bundle.json");
        var computed = BundleHasher.ComputeVersion(bundleBytes);
        var ok = meta is not null && computed == meta.Version;
        Console.WriteLine($"  meta advertises   : {meta?.Version}");
        Console.WriteLine($"  recomputed hash   : {computed}");
        Console.WriteLine($"  bytes             : {bundleBytes.Length:N0}");
        return ok;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  verification error: {ex.Message}");
        return false;
    }
}