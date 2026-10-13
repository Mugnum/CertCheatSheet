namespace Mugnum.CertCheatSheet.Models;

/// <summary>
/// Вариант ответа с исходным маркером.
/// </summary>
/// <param name="Marker"> Символ правильности либо дополнительный маркер. </param>
/// <param name="Text"> Текст с сохранёнными переносами строк. </param>
public sealed record CheatSheetAnswer(char Marker, string Text);
