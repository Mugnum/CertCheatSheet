using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using Mugnum.CertCheatSheet.Configuration;

namespace Mugnum.CertCheatSheet.Components.Layout;

/// <summary>
/// Общая навигация и область содержимого страниц.
/// </summary>
public partial class MainLayout
{
	/// <summary>
	/// Определяет доступность страницы добавления вопросов.
	/// </summary>
	[Inject]
	private IOptions<CheatSheetOptions> _options { get; set; } = null!;
}
