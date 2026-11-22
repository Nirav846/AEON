namespace Aeon.App.Pages;

/// <summary>
/// Modal progress surface shown while the update APK downloads. The download itself is
/// not tied to this page: if Android's back gesture closes it, the transfer finishes and
/// the installer prompt still appears, so the caller pops it only when it is still open.
/// </summary>
public partial class UpdateDownloadPage : ContentPage
{
    public UpdateDownloadPage(Version version)
    {
        InitializeComponent();
        Headline.Text = $"Downloading AEON {version}";
        Detail.Text = "Starting…";
    }

    /// <summary>Called from the download loop, which runs off the UI thread.</summary>
    public void Report(double fraction)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Bar.Progress = fraction;
            Detail.Text = $"{fraction:P0}";
        });
    }
}
