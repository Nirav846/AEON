using System.Text.Json;
using Aeon.App.Pages;

namespace Aeon.App.Services;

public sealed record UpdateInfo(Version Version, string ApkUrl);

public enum UpdateCheckStatus
{
    UpToDate,
    Available,
    Failed,
}

public sealed record UpdateCheck(UpdateCheckStatus Status, UpdateInfo? Update);

/// <summary>
/// Sideloaded self-update. Android gives no silent install path, so this only ever
/// offers the new APK: it reads the latest GitHub release, downloads the .apk into the
/// cache directory, and hands it to the system installer, which shows its own
/// "Update?" prompt. Nothing is installed without the user tapping through that prompt.
///
/// Three preconditions live outside this file and are easy to break silently:
///   1. every build is signed with the same keystore (a different key is a different
///      app to Android, and the install is refused);
///   2. ApplicationVersion rises on every release, which the workflow ties to the run
///      number, matching the v1.0.&lt;run&gt; tag read here;
///   3. the repository is public, so release assets can be read without a token.
///
/// The release JSON is parsed with JsonDocument rather than reflection-based
/// deserialisation: Release builds are trimmed, and reflection serialisers can lose
/// their members without any compile-time warning.
/// </summary>
public static class UpdateService
{
    private const string LatestReleaseUrl =
        "https://api.github.com/repos/Nirav846/AEON/releases/latest";

    private const string ApkFileName = "update.apk";

    private const string ApkMimeType = "application/vnd.android.package-archive";

    // No global timeout: a 40-80 MB APK on a slow connection legitimately takes
    // minutes. The metadata call has its own short deadline in CheckAsync.
    private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

    private static bool _checkedThisSession;
    private static bool _flowRunning;

    /// <summary>
    /// APK downloaded by an earlier run whose install was blocked by the unknown-sources
    /// permission. Kept in memory only: a new launch re-checks the release anyway.
    /// </summary>
    private static string? _pendingInstallPath;

    public static Version CurrentVersion =>
        Version.TryParse(AppInfo.Current.VersionString, out var version) ? version : new Version(0, 0);

    /// <summary>
    /// Whether Android will let this app ask the installer to run. Always true below
    /// API 26, where "install unknown apps" is a single device-wide setting instead of a
    /// per-app grant.
    /// </summary>
    public static bool CanInstallPackages
    {
        get
        {
#if ANDROID
            if (!OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                return true;
            }
            return Android.App.Application.Context.PackageManager!.CanRequestPackageInstalls();
#else
            return false;
#endif
        }
    }

    /// <summary>
    /// Returns the newest release when it is newer than the installed display version.
    /// Any network, rate-limit or parse problem reports Failed rather than UpToDate, so a
    /// manual check never claims the app is current when it could not actually ask.
    /// </summary>
    public static async Task<UpdateCheck> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(10));

            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
            // GitHub rejects requests without a User-Agent.
            request.Headers.Add("User-Agent", "AEON-App");
            request.Headers.Add("Accept", "application/vnd.github+json");

            using var response = await Http.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheck(UpdateCheckStatus.Failed, null);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: deadline.Token);
            var root = json.RootElement;

            if (!root.TryGetProperty("tag_name", out var tagElement) ||
                tagElement.GetString() is not { } tag ||
                !Version.TryParse(tag.TrimStart('v', 'V'), out var latest))
            {
                return new UpdateCheck(UpdateCheckStatus.Failed, null);
            }

            string? apkUrl = null;
            if (root.TryGetProperty("assets", out var assets) &&
                assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    if (!asset.TryGetProperty("name", out var nameElement) ||
                        !asset.TryGetProperty("browser_download_url", out var urlElement))
                    {
                        continue;
                    }

                    if (nameElement.GetString() is { } name &&
                        name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
                    {
                        apkUrl = urlElement.GetString();
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(apkUrl))
            {
                return new UpdateCheck(UpdateCheckStatus.Failed, null);
            }

            return latest > CurrentVersion
                ? new UpdateCheck(UpdateCheckStatus.Available, new UpdateInfo(latest, apkUrl))
                : new UpdateCheck(UpdateCheckStatus.UpToDate, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or IOException)
        {
            // Offline, rate-limited (60 requests/hour per IP) or a truncated response.
            return new UpdateCheck(UpdateCheckStatus.Failed, null);
        }
    }

    /// <summary>
    /// Checks once per app launch and, if a newer release exists, offers to download and
    /// install it. <paramref name="force"/> backs the manual "Update" button, which must
    /// work after the user has been sent to the unknown-sources settings screen.
    /// Never throws: it is called fire-and-forget from page lifecycle code.
    /// </summary>
    public static async Task PromptIfAvailableAsync(Page page, bool force = false)
    {
        if (_flowRunning || (_checkedThisSession && !force))
        {
            return;
        }

        _checkedThisSession = true;
        _flowRunning = true;

        try
        {
            // A previous run may have downloaded the APK and been sent to the
            // unknown-sources settings screen before installing it. Reuse that file
            // instead of pulling 40+ MB down a second time.
            if (_pendingInstallPath is { } pending && File.Exists(pending))
            {
                if (!CanInstallPackages)
                {
                    if (force)
                    {
                        await page.DisplayAlertAsync(
                            "Allow installs",
                            "Android still needs permission to install updates from AEON. Allow it on the screen that just opened, then open Browse and tap Update.",
                            "OK");
                    }
                    return;
                }

                if (Install(pending))
                {
                    _pendingInstallPath = null;
                }
                return;
            }

            var check = await CheckAsync();

            if (check.Status == UpdateCheckStatus.Failed)
            {
                if (force)
                {
                    await page.DisplayAlertAsync(
                        "Could not check",
                        "GitHub could not be reached. Try again when you have a connection.",
                        "OK");
                }
                return;
            }

            if (check.Update is not { } update)
            {
                if (force)
                {
                    await page.DisplayAlertAsync(
                        "Up to date", $"AEON {CurrentVersion} is the latest version.", "OK");
                }
                return;
            }

            var go = await page.DisplayAlertAsync(
                "Update available",
                $"AEON {update.Version} is available. You have {CurrentVersion}.",
                "Download",
                "Later");
            if (!go)
            {
                return;
            }

            var progressPage = new UpdateDownloadPage(update.Version);
            await page.Navigation.PushModalAsync(progressPage);

            string path;
            try
            {
                path = await DownloadAsync(update.ApkUrl, new Progress<double>(progressPage.Report));
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException)
            {
                await PopIfOpenAsync(page, progressPage);
                await page.DisplayAlertAsync(
                    "Download failed",
                    "The update could not be downloaded. Check your connection and try again.",
                    "OK");
                return;
            }

            await PopIfOpenAsync(page, progressPage);

            _pendingInstallPath = path;
            if (Install(path))
            {
                _pendingInstallPath = null;
            }
            else
            {
                await page.DisplayAlertAsync(
                    "Allow installs",
                    "Android needs permission to install updates from AEON. Allow it on the screen that just opened, then open Browse and tap Update — the download is already saved, so it will not run again.",
                    "OK");
            }
        }
        catch
        {
            // Nothing here is worth crashing a launch over. A missed update prompt is
            // always recoverable from the manual Update button.
        }
        finally
        {
            _flowRunning = false;
        }
    }

    /// <summary>
    /// Pops the progress page only if it is still on the modal stack. Android's back
    /// gesture can close it while the download runs, and popping twice throws.
    /// </summary>
    private static async Task PopIfOpenAsync(Page host, Page modal)
    {
        if (host.Navigation.ModalStack.Contains(modal))
        {
            await host.Navigation.PopModalAsync();
        }
    }

    /// <summary>
    /// Downloads the APK into the cache directory and reports 0..1 as it goes. The
    /// download URL redirects to a CDN host; HttpClient follows that by default.
    /// </summary>
    public static async Task<string> DownloadAsync(
        string url, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(FileSystem.CacheDirectory, ApkFileName);

        using var response = await Http.GetAsync(
            url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1L;

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = File.Create(path);

        var buffer = new byte[81920];
        long received = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            received += read;
            if (total > 0)
            {
                progress?.Report((double)received / total);
            }
        }

        return path;
    }

#if ANDROID
    /// <summary>
    /// Hands the downloaded APK to the system installer through the FileProvider declared
    /// in AndroidManifest.xml. Returns false when the user has not yet allowed installs
    /// from this app; that path opens the settings screen and leaves the caller to
    /// explain. Android always shows its own confirmation, so this is never silent.
    /// </summary>
    public static bool Install(string path)
    {
        var context = Android.App.Application.Context;

        // API 26 introduced the per-app unknown-sources permission and this check.
        if (OperatingSystem.IsAndroidVersionAtLeast(26) &&
            !context.PackageManager!.CanRequestPackageInstalls())
        {
            var settings = new Android.Content.Intent(
                Android.Provider.Settings.ActionManageUnknownAppSources,
                Android.Net.Uri.Parse($"package:{context.PackageName}"));
            settings.AddFlags(Android.Content.ActivityFlags.NewTask);
            context.StartActivity(settings);
            return false;
        }

        var uri = AndroidX.Core.Content.FileProvider.GetUriForFile(
            context, $"{context.PackageName}.fileprovider", new Java.IO.File(path));

        var intent = new Android.Content.Intent(Android.Content.Intent.ActionView);
        intent.SetDataAndType(uri, ApkMimeType);
        intent.AddFlags(Android.Content.ActivityFlags.GrantReadUriPermission |
                        Android.Content.ActivityFlags.NewTask);
        context.StartActivity(intent);
        return true;
    }
#else
    public static bool Install(string path) => false;
#endif
}
