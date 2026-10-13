using System.Text;
using Mugnum.CertCheatSheet.Configuration;
using Mugnum.CertCheatSheet.Models;

namespace Mugnum.CertCheatSheet.Parsing;

/// <summary>
/// Проверяет новые вопросы и формирует блоки в поддерживаемом формате.
/// </summary>
public static class CheatSheetSerializer
{
	/// <summary>
	/// Формирует блок с правильными ответами в начале, сохраняя относительный порядок.
	/// </summary>
	/// <param name="question"> Вопрос с заполненными вариантами ответа. </param>
	/// <param name="markers"> Допустимые символы начала ответа. </param>
	/// <returns> Текст блока с переносами CRLF и без завершающего разделителя. </returns>
	public static string SerializeQuestion(CheatSheetQuestion question, string markers)
	{
		ArgumentNullException.ThrowIfNull(question);
		ArgumentException.ThrowIfNullOrEmpty(markers);

		if (!CheatSheetOptions.AreMarkersValid(markers))
		{
			throw new ArgumentException("Недопустимый набор маркеров.", nameof(markers));
		}

		ValidateText(question.Text, "Вопрос", markers);

		if (question.Answers.IsDefaultOrEmpty)
		{
			throw new ArgumentException("Добавьте хотя бы один ответ.", nameof(question));
		}

		foreach (var answer in question.Answers)
		{
			ArgumentNullException.ThrowIfNull(answer);

			if (!markers.Contains(answer.Marker))
			{
				throw new ArgumentException($"Маркер «{answer.Marker}» отсутствует в настройках.", nameof(question));
			}
			ValidateText(answer.Text, "Ответ", markers);
		}

		var text = new StringBuilder("# ");
		text.Append(ToFileText(question.Text));
		var orderedAnswers = question.Answers.OrderByDescending(answer => answer.Marker == '+');
		foreach (var answer in orderedAnswers)
		{
			text.Append("\r\n");
			text.Append(answer.Marker);
			text.Append(' ');
			text.Append(ToFileText(answer.Text));
		}

		return text.ToString();
	}

	/// <summary>
	/// Исключает пустой текст и переносы, меняющие структуру блока при повторном чтении.
	/// </summary>
	/// <param name="text"> Проверяемый текст вопроса или ответа. </param>
	/// <param name="label"> Название поля для сообщения об ошибке. </param>
	/// <param name="markers"> Символы, недопустимые в начале немаркированной строки продолжения. </param>
	private static void ValidateText(string? text, string label, string markers)
	{
		if (text is null || string.IsNullOrEmpty(text.Trim()))
		{
			throw new ArgumentException($"{label}: заполните текст.");
		}

		var lines = CheatSheetParser.NormalizeLineEndings(text.Trim()).Split('\n');
		for (var index = 0; index < lines.Length; index++)
		{
			var line = lines[index];

			if (string.IsNullOrEmpty(line.Trim()))
			{
				throw new ArgumentException($"{label}: пустая строка служит разделителем вопросов.");
			}

			if (index > 0 && (markers.Contains(line[0]) || line.StartsWith("# ", StringComparison.Ordinal)))
			{
				throw new ArgumentException($"{label}: строка продолжения не должна начинаться с маркера. "
					+ "Добавьте перед ним пробел.");
			}
		}
	}

	/// <summary>
	/// Готовит текст поля к записи с переносами Windows.
	/// </summary>
	/// <param name="text"> Проверенный текст вопроса или ответа. </param>
	private static string ToFileText(string text)
	{
		return CheatSheetParser.NormalizeLineEndings(text.Trim()).Replace("\n", "\r\n", StringComparison.Ordinal);
	}
}
