using System.Collections.Immutable;
using Mugnum.CertCheatSheet.Configuration;
using Mugnum.CertCheatSheet.Models;

namespace Mugnum.CertCheatSheet.Parsing;

/// <summary>
/// Читает блоки вопросов, разделённые пустыми строками.
/// </summary>
public static class CheatSheetParser
{
	/// <summary>
	/// Разбирает текст, сохраняя порядок ответов и многострочные продолжения.
	/// </summary>
	/// <param name="source"> Содержимое файла после декодирования. </param>
	/// <param name="reservedAnswerMarkers"> Символы, начинающие ответ в первой колонке. </param>
	/// <returns> Вопросы и предупреждения о повреждённых блоках. </returns>
	public static CheatSheetParseResult Parse(string source, string reservedAnswerMarkers)
	{
		ArgumentNullException.ThrowIfNull(source);
		ArgumentException.ThrowIfNullOrEmpty(reservedAnswerMarkers);

		if (!CheatSheetOptions.AreMarkersValid(reservedAnswerMarkers))
		{
			throw new ArgumentException("Недопустимый набор маркеров.", nameof(reservedAnswerMarkers));
		}

		var questions = ImmutableArray.CreateBuilder<CheatSheetQuestion>();
		var warnings = ImmutableArray.CreateBuilder<string>();
		var lines = NormalizeLineEndings(source).TrimStart('\uFEFF').Split('\n');
		var block = new List<string>();
		var blockStart = 1;

		for (var lineIndex = 0; lineIndex <= lines.Length; lineIndex++)
		{
			var line = lineIndex < lines.Length
				? lines[lineIndex].TrimEnd()
				: string.Empty;

			if (string.IsNullOrEmpty(line))
			{
				if (block.Count > 0)
				{
					ParseBlock(block, blockStart, reservedAnswerMarkers, questions, warnings);
					block.Clear();
				}
				continue;
			}

			if (block.Count == 0)
			{
				blockStart = lineIndex + 1;
			}

			block.Add(line);
		}

		return new CheatSheetParseResult(questions.ToImmutable(), warnings.ToImmutable());
	}

	/// <summary>
	/// Приводит CRLF и CR к LF для единого чтения и проверки текста.
	/// </summary>
	/// <param name="text"> Исходный текст. </param>
	public static string NormalizeLineEndings(string text)
	{
		ArgumentNullException.ThrowIfNull(text);
		return text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
	}

	/// <summary>
	/// Немаркированные строки продолжают вопрос или последний ответ.
	/// </summary>
	/// <param name="lines"> Строки одного блока вопроса без пустых разделителей. </param>
	/// <param name="startLine"> Номер первой строки блока в исходном файле, начиная с единицы. </param>
	/// <param name="markers"> Допустимые символы начала ответа в первой колонке. </param>
	/// <param name="questions"> Коллекция, пополняемая распознанными вопросами. </param>
	/// <param name="warnings"> Коллекция сообщений о нарушениях формата с номерами строк. </param>
	private static void ParseBlock(
		List<string> lines,
		int startLine,
		string markers,
		ImmutableArray<CheatSheetQuestion>.Builder questions,
		ImmutableArray<string>.Builder warnings)
	{
		if (!lines[0].StartsWith("# ", StringComparison.Ordinal))
		{
			warnings.Add($"Строка {startLine}: блок пропущен, ожидается «# » перед вопросом.");
			return;
		}

		var questionLines = new List<string> { lines[0][2..] };
		var answerLines = new List<string>();
		var answers = ImmutableArray.CreateBuilder<CheatSheetAnswer>();
		char? marker = null;
		var answerStart = startLine;

		for (var index = 1; index < lines.Count; index++)
		{
			var line = lines[index];

			if (line.StartsWith("# ", StringComparison.Ordinal))
			{
				warnings.Add($"Строка {startLine + index}: перед следующим вопросом нужна пустая строка.");
				return;
			}

			if (markers.Contains(line[0]))
			{
				FlushAnswer(marker, answerLines, answerStart, answers, warnings);
				marker = line[0];
				answerStart = startLine + index;
				answerLines.Add(line[1..].TrimStart());
			}
			else if (marker is null)
			{
				questionLines.Add(line);
			}
			else
			{
				answerLines.Add(line);
			}
		}

		FlushAnswer(marker, answerLines, answerStart, answers, warnings);
		var questionText = string.Join('\n', questionLines).Trim();

		if (string.IsNullOrEmpty(questionText))
		{
			warnings.Add($"Строка {startLine}: пустой вопрос пропущен.");
			return;
		}

		if (answers.Count == 0)
		{
			warnings.Add($"Строка {startLine}: для вопроса не указаны ответы.");
		}

		questions.Add(new CheatSheetQuestion(questionText, answers.ToImmutable()));
	}

	/// <summary>
	/// Завершает текущий ответ и диагностирует отсутствие его текста.
	/// </summary>
	/// <param name="marker"> Маркер текущего ответа либо null, если ответ ещё не начат. </param>
	/// <param name="lines"> Строки текущего ответа; очищаются после его обработки. </param>
	/// <param name="startLine"> Номер первой строки ответа в исходном файле. </param>
	/// <param name="answers"> Коллекция, пополняемая ответами с непустым текстом. </param>
	/// <param name="warnings"> Коллекция сообщений о пустых ответах. </param>
	private static void FlushAnswer(
		char? marker,
		List<string> lines,
		int startLine,
		ImmutableArray<CheatSheetAnswer>.Builder answers,
		ImmutableArray<string>.Builder warnings)
	{
		if (marker is null)
		{
			return;
		}

		var text = string.Join('\n', lines).Trim();

		if (string.IsNullOrEmpty(text))
		{
			warnings.Add($"Строка {startLine}: пустой ответ пропущен.");
		}
		else
		{
			answers.Add(new CheatSheetAnswer(marker.Value, text));
		}

		lines.Clear();
	}
}
