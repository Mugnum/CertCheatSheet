using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Mugnum.CertCheatSheet.Components.Pages;
using Mugnum.CertCheatSheet.Components.Shared;
using Mugnum.CertCheatSheet.Configuration;
using Mugnum.CertCheatSheet.Models;
using Mugnum.CertCheatSheet.Storage;

namespace Mugnum.CertCheatSheet.Tests;

/// <summary>
/// Проверки серверного HTML без JavaScript и соединения с Blazor.
/// </summary>
public sealed class RenderingTests
{
	/// <summary>
	/// Все вопросы и ответы присутствуют в начальном HTML и доступны браузерному поиску.
	/// </summary>
	[Fact]
	public async Task RenderAllQuestionsWithoutPagination()
	{
		const int QuestionCount = 126;
		var sourcePath = Path.Combine(Path.GetTempPath(), $"CertCheatSheet.Rendering.{Guid.NewGuid():N}.txt");
		var blocks = Enumerable.Range(1, QuestionCount)
			.Select(number => $"# Вопрос {number}\n+ Ответ {number}");

		try
		{
			await File.WriteAllTextAsync(sourcePath, string.Join("\n\n", blocks));
			var builder = Host.CreateApplicationBuilder();
			builder.Services.Configure<CheatSheetOptions>(options => options.FilePath = sourcePath);
			builder.Services.AddSingleton<CheatSheetStore>();
			using var host = builder.Build();
			await host.Services.GetRequiredService<CheatSheetStore>().ReloadAsync();
			await using var renderer = new HtmlRenderer(host.Services, host.Services.GetRequiredService<ILoggerFactory>());
			var html = await renderer.Dispatcher.InvokeAsync(async () =>
			{
				var component = await renderer.RenderComponentAsync<Home>();
				return component.ToHtmlString();
			});
			var text = System.Net.WebUtility.HtmlDecode(html);

			Assert.Equal(QuestionCount, System.Text.RegularExpressions.Regex.Matches(html, "<article class=\"question-row\"").Count);
			Assert.Contains($"Вопрос {QuestionCount}", text);
			Assert.Contains($"Ответ {QuestionCount}", text);
			Assert.DoesNotContain("pagination", html);
		}
		finally
		{
			File.Delete(sourcePath);
		}
	}

	/// <summary>
	/// Вопросы и ответы присутствуют в начальном HTML, а опасный текст экранируется.
	/// </summary>
	[Fact]
	public async Task RenderQuestionBeforeConnectingBrowser()
	{
		var question = new CheatSheetQuestion("<script>alert(1)</script>",
		[
			new CheatSheetAnswer('+', "Да"),
			new CheatSheetAnswer('-', "Нет")
		]);
		var html = await RenderCardAsync(question, true);

		Assert.Contains("&lt;script&gt;", html);
		Assert.DoesNotContain("<script>", html);
		Assert.Contains("Да", System.Net.WebUtility.HtmlDecode(html));
		Assert.Contains("correct", html);
		Assert.Contains("incorrect-red", html);
	}

	/// <summary>
	/// Минусы не становятся неправильными, если в вопросе отсутствует плюс.
	/// </summary>
	[Fact]
	public async Task RenderUnknownAnswersWithoutRed()
	{
		var html = await RenderCardAsync(new CheatSheetQuestion("Вопрос", [new CheatSheetAnswer('-', "Вариант")]), true);
		Assert.Contains("answer-icon unknown", html);
		Assert.DoesNotContain("incorrect", html);
	}

	/// <summary>
	/// Настройка выключает красный крест, сохраняя зелёный правильный вариант.
	/// </summary>
	[Fact]
	public async Task RenderIncorrectAnswersWithNeutralIcon()
	{
		var html = await RenderCardAsync(new CheatSheetQuestion("Вопрос",
		[
			new CheatSheetAnswer('+', "Да"),
			new CheatSheetAnswer('-', "Нет")
		]), false);
		Assert.Contains("answer-icon incorrect", html);
		Assert.Contains("answer-icon correct", html);
		Assert.DoesNotContain("incorrect-red", html);
	}

	/// <summary>
	/// Рендерит карточку средствами ASP.NET без браузерного окружения.
	/// </summary>
	/// <param name="question"> Вопрос, отображаемый тестовым компонентом. </param>
	/// <param name="showRed"> Признак красного выделения неправильных ответов. </param>
	private static async Task<string> RenderCardAsync(CheatSheetQuestion question, bool showRed)
	{
		var services = new ServiceCollection();
		services.AddLogging();
		await using var provider = services.BuildServiceProvider();
		await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
		return await renderer.Dispatcher.InvokeAsync(async () =>
		{
			var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
			{
				[nameof(QuestionCard.Question)] = question,
				[nameof(QuestionCard.Number)] = 1,
				[nameof(QuestionCard.ShowIncorrectAnswersAsRed)] = showRed
			});
			var component = await renderer.RenderComponentAsync<QuestionCard>(parameters);
			return component.ToHtmlString();
		});
	}
}
