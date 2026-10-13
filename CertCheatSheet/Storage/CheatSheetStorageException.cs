namespace Mugnum.CertCheatSheet.Storage;

/// <summary>
/// Ошибка хранения с сообщением для интерфейса и исходным исключением для журнала.
/// </summary>
public sealed class CheatSheetStorageException : Exception
{
	/// <summary>
	/// Сохраняет причину отказа записи.
	/// </summary>
	/// <param name="message"> Понятное пользователю сообщение. </param>
	/// <param name="innerException"> Исходная ошибка файловой системы или формата. </param>
	public CheatSheetStorageException(string message, Exception innerException)
		: base(message, innerException)
	{
		ArgumentException.ThrowIfNullOrEmpty(message);
		ArgumentNullException.ThrowIfNull(innerException);
	}
}
