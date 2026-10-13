using Microsoft.AspNetCore.Components;
using Mugnum.CertCheatSheet.Models;

namespace Mugnum.CertCheatSheet.Components.Shared;

/// <summary>
/// Показывает вопрос и правильность вариантов с доступными текстовыми обозначениями.
/// </summary>
public partial class QuestionCard
{
	/// <summary>
	/// Вопрос из текущего снимка файла.
	/// </summary>
	[Parameter, EditorRequired]
	public CheatSheetQuestion Question { get; set; } = null!;

	/// <summary>
	/// Позиция вопроса в исходном файле.
	/// </summary>
	[Parameter]
	public int Number { get; set; }

	/// <summary>
	/// Показывать красный крест вместо нейтрального квадрата для минуса.
	/// </summary>
	[Parameter]
	public bool ShowIncorrectAnswersAsRed { get; set; }

	/// <summary>
	/// Не считает минус ошибкой, если для вопроса отсутствует подтверждённый плюс.
	/// </summary>
	/// <param name="answer"> Вариант ответа, для которого выбираются значок и доступное описание. </param>
	private (string CssClass, string Symbol, string Label) GetAnswerAppearance(CheatSheetAnswer answer)
	{
		if (!Question.HasKnownCorrectAnswer)
		{
			return ("unknown", "?", "Правильность не подтверждена");
		}

		return answer.Marker switch
		{
			'+' => ("correct", "✓", "Правильный ответ"),
			'-' => ShowIncorrectAnswersAsRed
				? ("incorrect-red", "×", "Неправильный ответ")
				: ("incorrect", string.Empty, "Неправильный ответ"),
			_ => ("custom", answer.Marker.ToString(), $"Дополнительный маркер {answer.Marker}")
		};
	}
}
