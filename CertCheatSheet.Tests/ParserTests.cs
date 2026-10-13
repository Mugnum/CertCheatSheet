using Mugnum.CertCheatSheet.Models;
using Mugnum.CertCheatSheet.Parsing;

namespace Mugnum.CertCheatSheet.Tests;

/// <summary>
/// Проверки контракта существующего текстового формата.
/// </summary>
public sealed class ParserTests
{
	/// <summary>
	/// Читает разные переносы, BOM, пробельные разделители и несколько правильных ответов.
	/// </summary>
	/// <param name="newline"> Проверяемая последовательность перевода строки. </param>
	[Theory]
	[InlineData("\r\n")]
	[InlineData("\n")]
	[InlineData("\r")]
	public void ParseQuestionsWithDifferentLineEndings(string newline)
	{
		var source = string.Join(newline, "\uFEFF# Первый?", "+ Да", "+ Ещё", "- Нет", " \t", "",
			"# Второй?", "- Неизвестно");
		var result = CheatSheetParser.Parse(source, "+-");

		Assert.Empty(result.Warnings);
		Assert.Equal(2, result.Questions.Length);
		Assert.Equal("Первый?", result.Questions[0].Text);
		Assert.Equal(['+', '+', '-'], result.Questions[0].Answers.Select(answer => answer.Marker));
		Assert.True(result.Questions[0].HasKnownCorrectAnswer);
		Assert.False(result.Questions[1].HasKnownCorrectAnswer);
	}

	/// <summary>
	/// Сохраняет продолжения и пользовательские маркеры без перестановки ответов при чтении.
	/// </summary>
	[Fact]
	public void ParseMultilineQuestionAndAnswers()
	{
		var result = CheatSheetParser.Parse("# Вопрос\nпродолжение\n- Вариант\nстрока ответа\n~ Пометка\n+ Верно", "+-~");

		Assert.Empty(result.Warnings);
		Assert.Equal("Вопрос\nпродолжение", result.Questions[0].Text);
		Assert.Equal("Вариант\nстрока ответа", result.Questions[0].Answers[0].Text);
		Assert.Equal(['-', '~', '+'], result.Questions[0].Answers.Select(answer => answer.Marker));
	}

	/// <summary>
	/// Диагностирует повреждённый блок и продолжает чтение следующего корректного блока.
	/// </summary>
	[Fact]
	public void ParseReportsSourceLineForMalformedBlock()
	{
		var result = CheatSheetParser.Parse("Нет заголовка\n- Ответ\n\n# Верный блок\n+ Да", "+-");

		Assert.Single(result.Questions);
		Assert.Contains("Строка 1", result.Warnings[0]);
	}

	/// <summary>
	/// Не поглощает следующий вопрос как продолжение ответа при пропущенном разделителе.
	/// </summary>
	[Fact]
	public void ParseReportsMissingBlockSeparator()
	{
		var result = CheatSheetParser.Parse("# Один\n+ Да\n# Два\n- Нет", "+-");

		Assert.Empty(result.Questions);
		Assert.Contains("Строка 3", Assert.Single(result.Warnings));
	}

	/// <summary>
	/// Не выдаёт пустой вопрос или пустой ответ за корректные данные.
	/// </summary>
	/// <param name="source"> Блок с отсутствующим текстом вопроса или ответа. </param>
	[Theory]
	[InlineData("# \n+ Текст")]
	[InlineData("# Вопрос\n+ ")]
	[InlineData("# Вопрос")]
	public void ParseReportsEmptyContent(string source)
	{
		Assert.NotEmpty(CheatSheetParser.Parse(source, "+-").Warnings);
	}

	/// <summary>
	/// Пустой файл допустим до первого добавления через форму.
	/// </summary>
	[Fact]
	public void ParseEmptyFile()
	{
		var result = CheatSheetParser.Parse("\uFEFF \r\n\t", "+-");
		Assert.Empty(result.Questions);
		Assert.Empty(result.Warnings);
	}

	/// <summary>
	/// Не разрешает наборы маркеров, противоречащие структуре файла.
	/// </summary>
	/// <param name="markers"> Недопустимый набор маркеров ответов. </param>
	[Theory]
	[InlineData("+")]
	[InlineData("+-#")]
	[InlineData("+- ")]
	public void ParseRejectsInvalidMarkers(string markers)
	{
		Assert.Throws<ArgumentException>(() => CheatSheetParser.Parse("# Вопрос\n+ Да", markers));
	}

	/// <summary>
	/// Новый блок ставит плюсы первыми; повторное чтение сохраняет все тексты и метки.
	/// </summary>
	[Fact]
	public void SerializeSortsCorrectAnswersAndRoundTrips()
	{
		var question = new CheatSheetQuestion("Вопрос\nпродолжение",
			[new CheatSheetAnswer('-', "Нет"), new CheatSheetAnswer('+', "Да\nдетали"), new CheatSheetAnswer('~', "Пометка"), new CheatSheetAnswer('+', "Тоже да")]);
		var block = CheatSheetSerializer.SerializeQuestion(question, "+-~");
		var result = CheatSheetParser.Parse(block, "+-~");

		Assert.Empty(result.Warnings);
		Assert.Equal(question.Text, result.Questions[0].Text);
		Assert.Equal(["Да\nдетали", "Тоже да", "Нет", "Пометка"],
			result.Questions[0].Answers.Select(answer => answer.Text));
		Assert.Contains("\r\n+ Да\r\nдетали\r\n+ Тоже да\r\n- Нет", block);
	}

	/// <summary>
	/// Не записывает текст, в котором переносы создают новые блоки или варианты.
	/// </summary>
	/// <param name="text"> Текст вопроса для проверки структуры блока. </param>
	/// <param name="answer"> Текст ответа для проверки структуры блока. </param>
	[Theory]
	[InlineData("Вопрос\n\nВторой блок", "Ответ")]
	[InlineData("Вопрос\n+ Скрытый ответ", "Ответ")]
	[InlineData("Вопрос", "Ответ\n- Подменённый вариант")]
	[InlineData("Вопрос", "Ответ\n# Подменённый вопрос")]
	[InlineData("   ", "Ответ")]
	[InlineData("Вопрос", "  ")]
	public void SerializeRejectsStructuralInjection(string text, string answer)
	{
		var question = new CheatSheetQuestion(text, [new CheatSheetAnswer('+', answer)]);
		Assert.Throws<ArgumentException>(() => CheatSheetSerializer.SerializeQuestion(question, "+-"));
	}
}
