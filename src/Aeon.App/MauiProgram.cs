using Aeon.App.Pages;
using Aeon.App.Services;
using Aeon.Data;

namespace Aeon.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddSingleton<BundleFetcher>();
		builder.Services.AddSingleton<BundleService>();
		builder.Services.AddSingleton<SportsPage>();
		builder.Services.AddSingleton<SearchPage>();

		return builder.Build();
	}
}