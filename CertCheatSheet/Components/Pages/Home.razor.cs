using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using Mugnum.CertCheatSheet.Configuration;
using Mugnum.CertCheatSheet.Models;
using Mugnum.CertCheatSheet.Storage;

namespace Mugnum.CertCheatSheet.Components.Pages;

/// <summary>
/// Поиск и просмотр всех вопросов с серверным начальным рендерингом.
/// </summary>
public partial class Home
{
	/// <summary>
	/// Общее файловое хранилище.
	/// </summary>
	[Inject]
	private CheatSheetStore _store { get; set; } = null!;

	/// <summary>
	/// Начальные настройки отображения.
	/// </summary>
	[Inject]
	private IOptions<CheatSheetOptions> _options { get; set; } = null!;

	/// <summary>
	/// Версия данных, согласованная со счётчиками и диагностикой.
	/// </summary>
	private CheatSheetSnapshot _snapshot = null!;

	/// <summary>
	/// Строка поиска без изменения исходных текстов.
	/// </summary>
	private string _search = string.Empty;

	/// <summary>
	/// Операция перечитывания файла выполняется.
	/// </summary>
	private bool _isReloading;

	/// <summary>
	/// Индивидуальная настройка выделения минусов в текущей сессии.
	/// </summary>
	private bool _showIncorrectAsRed;

	/// <summary>
	/// Ограничить результаты вопросами без плюса.
	/// </summary>
	private bool _onlyUnknown;

	/// <summary>
	/// Результаты поиска с исходной нумерацией.
	/// </summary>
	private (CheatSheetQuestion Question, int Number)[] _filteredQuestions = [];

	/// <inheritdoc />
	/// <summary>
	/// Получает один снимок до первого рендеринга.
	/// </summary>
	protected override void OnInitialized()
	{
		_snapshot = _store.Snapshot;
		_showIncorrectAsRed = _options.Value.ShowIncorrectAnswersAsRed;
		ApplyFilter();
	}

	/// <summary>
	/// Обновляет поиск и список отображаемых вопросов.
	/// </summary>
	/// <param name="args"> Событие ввода с новым значением строки поиска. </param>
	private void ChangeSearch(ChangeEventArgs args)
	{
		_search = args.Value?.ToString() ?? string.Empty;
		ApplyFilter();
	}

	/// <summary>
	/// Обновляет фильтр вопросов с неподтверждённой правильностью.
	/// </summary>
	/// <param name="args"> Событие изменения флажка фильтра с булевым значением. </param>
	private void ChangeUnknownFilter(ChangeEventArgs args)
	{
		_onlyUnknown = args.Value is true;
		ApplyFilter();
	}

	/// <summary>
	/// Перечитывает источник и обновляет согласованный снимок этой страницы.
	/// </summary>
	private async Task ReloadFileAsync()
	{
		if (_isReloading)
		{
			return;
		}

		_isReloading = true;
		try
		{
			await _store.ReloadAsync();
			_snapshot = _store.Snapshot;
			ApplyFilter();
		}
		finally
		{
			_isReloading = false;
		}
	}

	/// <summary>
	/// Ищет без учёта регистра по вопросу и всем вариантам ответа.
	/// </summary>
	private void ApplyFilter()
	{
		var search = _search.Trim();
		_filteredQuestions =
		[
			.. _snapshot.Questions
				.Select((question, index) => (Question: question, Number: index + 1))
				.Where(item => (!_onlyUnknown || !item.Question.HasKnownCorrectAnswer)
				               && (string.IsNullOrEmpty(search)
				                   || item.Question.Text.Contains(search, StringComparison.OrdinalIgnoreCase)
				                   || item.Question.Answers.Any(answer =>
					                   answer.Text.Contains(search, StringComparison.OrdinalIgnoreCase))))
		];
	}
}
