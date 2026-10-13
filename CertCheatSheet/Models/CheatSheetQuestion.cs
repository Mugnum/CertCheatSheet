using System.Collections.Immutable;

namespace Mugnum.CertCheatSheet.Models;

/// <summary>
/// Вопрос и неизменяемый набор вариантов ответа.
/// </summary>
/// <param name="Text"> Текст вопроса. </param>
/// <param name="Answers"> Ответы в порядке, указанном в файле. </param>
public sealed record CheatSheetQuestion(string Text, ImmutableArray<CheatSheetAnswer> Answers)
{
	/// <summary>
	/// Правильность известна, если среди ответов есть хотя бы один плюс.
	/// </summary>
	public bool HasKnownCorrectAnswer => Answers.Any(answer => answer.Marker == '+');
}
