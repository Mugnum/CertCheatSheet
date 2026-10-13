using System.Collections.Immutable;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using Mugnum.CertCheatSheet.Configuration;
using Mugnum.CertCheatSheet.Models;
using Mugnum.CertCheatSheet.Storage;

namespace Mugnum.CertCheatSheet.Components.Pages;

/// <summary>
/// Добавляет один вопрос, сохраняя введённые значения при отказе записи.
/// </summary>
public partial class NewQuestion
{
	/// <summary>
	/// Файловое хранилище с последовательной записью.
	/// </summary>
	[Inject]
	private CheatSheetStore _store { get; set; } = null!;

	/// <summary>
	/// Доступность записи и дополнительные маркеры.
	/// </summary>
	[Inject]
	private IOptions<CheatSheetOptions> _options { get; set; } = null!;

	/// <summary>
	/// Введённый текст вопроса.
	/// </summary>
	private string _questionText = string.Empty;

	/// <summary>
	/// Редактируемые варианты с устойчивыми идентификаторами компонентов.
	/// </summary>
	private readonly List<AnswerDraft> _answers = [new(), new()];

	/// <summary>
	/// Маркеры, отличные от плюса и минуса.
	/// </summary>
	private char[] _customMarkers = [];

	/// <summary>
	/// Предотвращает повторное сохранение до завершения записи.
	/// </summary>
	private bool _isSaving;

	/// <summary>
	/// Диагностика последнего отказа.
	/// </summary>
	private string? _error;

	/// <summary>
	/// Подтверждение успешного добавления.
	/// </summary>
	private string? _success;

	/// <inheritdoc />
	/// <summary>
	/// Подготавливает доступные метки из конфигурации.
	/// </summary>
	protected override void OnInitialized()
	{
		_customMarkers =
		[
			.. _options.Value.ReservedAnswerMarkers
				.Where(marker => marker is not '+' and not '-')
				.Distinct()
		];
	}

	/// <summary>
	/// Добавляет пустой вариант без влияния на уже введённые значения.
	/// </summary>
	private void AddAnswer()
	{
		_answers.Add(new AnswerDraft());
	}

	/// <summary>
	/// Удаляет выбранный вариант, оставляя хотя бы один ответ.
	/// </summary>
	/// <param name="answer"> Удаляемый вариант из текущей формы. </param>
	private void RemoveAnswer(AnswerDraft answer)
	{
		if (_answers.Count > 1)
		{
			_answers.Remove(answer);
		}
	}

	/// <summary>
	/// Проверяет и записывает вопрос; очищает форму только после успешной записи.
	/// </summary>
	private async Task SaveQuestionAsync()
	{
		if (_isSaving || !_options.Value.EnableEditing)
		{
			return;
		}

		_isSaving = true;
		_error = null;
		_success = null;
		try
		{
			var answers = _answers
				.Select(answer => new CheatSheetAnswer(answer.Marker[0], answer.Text))
				.ToImmutableArray();
			var question = new CheatSheetQuestion(_questionText, answers);
			await _store.AddQuestionAsync(question);
			_questionText = string.Empty;
			_answers.Clear();
			_answers.Add(new AnswerDraft());
			_answers.Add(new AnswerDraft());
			_success = "Вопрос добавлен в файл.";
		}
		catch (ArgumentException exception)
		{
			_error = exception.Message;
		}
		catch (CheatSheetStorageException exception)
		{
			_error = exception.Message;
		}
		finally
		{
			_isSaving = false;
		}
	}

	/// <summary>
	/// Значения одного варианта в форме до создания неизменяемой модели.
	/// </summary>
	private sealed class AnswerDraft
	{
		/// <summary>
		/// Стабильный ключ DOM при удалении соседних вариантов.
		/// </summary>
		public Guid Id { get; } = Guid.NewGuid();

		/// <summary>
		/// Выбранный маркер, совместимый с HTML select.
		/// </summary>
		public string Marker { get; set; } = "-";

		/// <summary>
		/// Введённый текст варианта.
		/// </summary>
		public string Text { get; set; } = string.Empty;
	}
}
