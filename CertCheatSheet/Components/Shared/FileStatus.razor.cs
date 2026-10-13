using Microsoft.AspNetCore.Components;
using Mugnum.CertCheatSheet.Models;

namespace Mugnum.CertCheatSheet.Components.Shared;

/// <summary>
/// Сообщения о чтении и нарушениях формата источника.
/// </summary>
public partial class FileStatus
{
	/// <summary>
	/// Состояние той же версии файла, которая показана на странице.
	/// </summary>
	[Parameter, EditorRequired]
	public CheatSheetSnapshot Snapshot { get; set; } = null!;
}
