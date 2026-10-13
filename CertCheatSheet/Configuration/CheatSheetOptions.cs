namespace Mugnum.CertCheatSheet.Configuration;

/// <summary>
/// Параметры файла ответов и доступных действий пользователя.
/// </summary>
public sealed class CheatSheetOptions
{
	/// <summary>
	/// Имя раздела конфигурации.
	/// </summary>
	public const string SectionName = "CheatSheet";

	/// <summary>
	/// Абсолютный путь либо путь относительно каталога приложения.
	/// </summary>
	public string? FilePath { get; set; } = "bpm-answers.txt";

	/// <summary>
	/// Символы начала ответа; дополнительные символы не определяют правильность.
	/// </summary>
	public string ReservedAnswerMarkers { get; set; } = "+-";

	/// <summary>
	/// Выделять минус красным только при наличии хотя бы одного плюса в вопросе.
	/// </summary>
	public bool ShowIncorrectAnswersAsRed { get; set; } = true;

	/// <summary>
	/// Разрешить добавление вопросов через интерфейс.
	/// </summary>
	public bool EnableEditing { get; set; } = true;

	/// <summary>
	/// Проверяет обязательные маркеры и отсутствие конфликтов с форматом вопроса.
	/// </summary>
	/// <param name="markers"> Набор символов начала ответа. </param>
	/// <returns> Признак пригодности набора для чтения и записи. </returns>
	public static bool AreMarkersValid(string? markers)
	{
		return markers is not null
			&& markers.Contains('+')
			&& markers.Contains('-')
			&& !markers.Contains('#')
			&& !markers.Any(char.IsWhiteSpace);
	}
}
