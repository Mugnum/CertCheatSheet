using System.Collections.Immutable;

namespace Mugnum.CertCheatSheet.Models;

/// <summary>
/// Согласованное состояние последнего чтения файла.
/// </summary>
/// <param name="Questions"> Последний успешно прочитанный набор вопросов. </param>
/// <param name="SourcePath"> Полный путь к источнику. </param>
/// <param name="LoadedAt"> Время последнего успешного чтения. </param>
/// <param name="Error"> Понятное пользователю описание ошибки чтения. </param>
/// <param name="Warnings"> Ошибки формата с номерами строк. </param>
public sealed record CheatSheetSnapshot(
	ImmutableArray<CheatSheetQuestion> Questions,
	string SourcePath,
	DateTimeOffset? LoadedAt,
	string? Error,
	ImmutableArray<string> Warnings)
{
	/// <summary>
	/// Создаёт начальное состояние до чтения указанного источника.
	/// </summary>
	/// <param name="sourcePath"> Разрешённый путь к файлу. </param>
	public static CheatSheetSnapshot CreateEmpty(string sourcePath)
	{
		ArgumentException.ThrowIfNullOrEmpty(sourcePath);
		return new CheatSheetSnapshot([], sourcePath, null, null, []);
	}
}
