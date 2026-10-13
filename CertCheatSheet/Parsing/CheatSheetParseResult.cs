using System.Collections.Immutable;
using Mugnum.CertCheatSheet.Models;

namespace Mugnum.CertCheatSheet.Parsing;

/// <summary>
/// Прочитанные вопросы и диагностика нарушений формата.
/// </summary>
/// <param name="Questions"> Корректно распознанные блоки. </param>
/// <param name="Warnings"> Диагностика с номерами исходных строк. </param>
public sealed record CheatSheetParseResult(
	ImmutableArray<CheatSheetQuestion> Questions,
	ImmutableArray<string> Warnings);
