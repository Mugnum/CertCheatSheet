using System.Diagnostics;
using Mugnum.CertCheatSheet.Components;
using Mugnum.CertCheatSheet.Configuration;
using Mugnum.CertCheatSheet.Storage;

namespace Mugnum.CertCheatSheet;

/// <summary>
/// Настраивает сервер, файловое хранилище и компоненты приложения.
/// </summary>
public static class Program
{
	/// <summary>
	/// Запускает приложение с настройками из каталога исполняемого файла.
	/// </summary>
	/// <param name="args"> Параметры командной строки, переопределяющие конфигурацию. </param>
	public static async Task Main(string[] args)
	{
		ArgumentNullException.ThrowIfNull(args);

		var builder = WebApplication.CreateBuilder(new WebApplicationOptions
		{
			Args = args,
			ContentRootPath = AppContext.BaseDirectory
		});

		builder.WebHost.UseStaticWebAssets();
		builder.WebHost.UseUrls(builder.Configuration["App:Url"] ?? "http://127.0.0.1:5088");
		builder.Services.AddRazorComponents().AddInteractiveServerComponents();
		builder.Services.AddOptions<CheatSheetOptions>()
			.Bind(builder.Configuration.GetSection(CheatSheetOptions.SectionName))
			.Validate(options => !string.IsNullOrEmpty(options.FilePath?.Trim()), "Не задан путь к файлу ответов.")
			.Validate(options => CheatSheetOptions.AreMarkersValid(options.ReservedAnswerMarkers),
				"Маркеры должны содержать + и -, без # и пробельных символов.")
			.ValidateOnStart();
		builder.Services.AddSingleton<CheatSheetStore>();

		var app = builder.Build();

		if (!app.Environment.IsDevelopment())
		{
			app.UseExceptionHandler("/error", createScopeForErrors: true);
		}

		app.UseStatusCodePagesWithReExecute("/not-found");
		app.UseAntiforgery();
		app.MapStaticAssets();
		app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

		var store = app.Services.GetRequiredService<CheatSheetStore>();
		await store.ReloadAsync();

		if (builder.Configuration.GetValue("App:OpenBrowser", true))
		{
			app.Lifetime.ApplicationStarted.Register(() => OpenBrowser(app));
		}

		await app.RunAsync();
	}

	/// <summary>
	/// Открывает адрес работающего сервера в системном браузере.
	/// </summary>
	/// <param name="app"> Запущенное приложение, предоставляющее адрес сервера и журнал ошибок. </param>
	private static void OpenBrowser(WebApplication app)
	{
		var address = app.Urls.FirstOrDefault();
		
		if (string.IsNullOrEmpty(address))
		{
			return;
		}

		try
		{
			Process.Start(new ProcessStartInfo(address)
			{
				UseShellExecute = true
			});
		}
		catch (Exception exception)
		{
			app.Logger.LogWarning(exception, "Не удалось открыть браузер для {Address}.", address);
		}
	}
}
