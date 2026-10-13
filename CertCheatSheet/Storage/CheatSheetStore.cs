using System.Text;
using Microsoft.Extensions.Options;
using Mugnum.CertCheatSheet.Configuration;
using Mugnum.CertCheatSheet.Models;
using Mugnum.CertCheatSheet.Parsing;

namespace Mugnum.CertCheatSheet.Storage;

/// <summary>
/// Хранит неизменяемый снимок файла и последовательно выполняет чтение и добавление.
/// </summary>
public sealed class CheatSheetStore : IDisposable
{
	/// <summary>
	/// Строгое декодирование UTF-8 без автоматического перехода к UTF-16.
	/// </summary>
	private static readonly UTF8Encoding Utf8 = new(false, true);

	/// <summary>
	/// Настройки источника и разрешённых операций.
	/// </summary>
	private readonly CheatSheetOptions _options;

	/// <summary>
	/// Журнал полных исключений файловой системы.
	/// </summary>
	private readonly ILogger<CheatSheetStore> _logger;

	/// <summary>
	/// Общий барьер чтения и записи для всех пользовательских сессий.
	/// </summary>
	private readonly SemaphoreSlim _fileLock = new(1, 1);

	/// <summary>
	/// Последнее согласованное состояние, публикуемое целиком.
	/// </summary>
	private CheatSheetSnapshot _snapshot;

	/// <summary>
	/// Полный путь, не зависящий от рабочего каталога процесса.
	/// </summary>
	public string SourcePath { get; }

	/// <summary>
	/// Текущее состояние; не изменяется читателями.
	/// </summary>
	public CheatSheetSnapshot Snapshot => Volatile.Read(ref _snapshot);

	/// <summary>
	/// Разрешает путь относительно корня приложения.
	/// </summary>
	/// <param name="options"> Параметры файла. </param>
	/// <param name="environment"> Каталог приложения. </param>
	/// <param name="logger"> Стандартный журнал приложения. </param>
	public CheatSheetStore(
		IOptions<CheatSheetOptions> options,
		IHostEnvironment environment,
		ILogger<CheatSheetStore> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(environment);
		ArgumentNullException.ThrowIfNull(logger);
		_options = options.Value;
		_logger = logger;
		var configuredPath = Environment.ExpandEnvironmentVariables(_options.FilePath?.Trim() ?? "");
		ArgumentException.ThrowIfNullOrEmpty(configuredPath);
		SourcePath = Path.GetFullPath(configuredPath, environment.ContentRootPath);
		_snapshot = CheatSheetSnapshot.CreateEmpty(SourcePath);
	}

	/// <summary>
	/// Перечитывает файл, сохраняя предыдущие данные при отказе чтения.
	/// </summary>
	/// <param name="cancellationToken"> Сигнал отмены ожидания и чтения. </param>
	public async Task ReloadAsync(CancellationToken cancellationToken = default)
	{
		await _fileLock.WaitAsync(cancellationToken);
		try
		{
			var bytes = await File.ReadAllBytesAsync(SourcePath, cancellationToken);
			PublishSnapshot(Utf8.GetString(bytes));
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DecoderFallbackException)
		{
			_logger.LogError(exception, "Не удалось прочитать {SourcePath}.", SourcePath);
			Volatile.Write(ref _snapshot, Snapshot with { Error = GetFileError(exception) });
		}
		finally
		{
			_fileLock.Release();
		}
	}

	/// <summary>
	/// Добавляет блок, сохраняя исходные байты и заменяя файл после полной записи.
	/// </summary>
	/// <param name="question"> Новый вопрос; плюсы записываются перед остальными ответами. </param>
	/// <param name="cancellationToken"> Отмена до завершения записи временного файла. </param>
	public async Task AddQuestionAsync(CheatSheetQuestion question, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(question);

		if (!_options.EnableEditing)
		{
			throw new InvalidOperationException("Добавление вопросов отключено в настройках приложения.");
		}

		var block = CheatSheetSerializer.SerializeQuestion(question, _options.ReservedAnswerMarkers);
		await _fileLock.WaitAsync(cancellationToken);
		string? temporaryPath = null;

		try
		{
			var directory = Path.GetDirectoryName(SourcePath)!;
			Directory.CreateDirectory(directory);
			await using var writeLease = new FileStream(SourcePath + ".lock", FileMode.OpenOrCreate,
				FileAccess.ReadWrite, FileShare.None);
			var bytes = File.Exists(SourcePath)
				? await File.ReadAllBytesAsync(SourcePath, cancellationToken)
				: [];
			var source = Utf8.GetString(bytes);
			var parsed = CheatSheetParser.Parse(source, _options.ReservedAnswerMarkers);

			if (!parsed.Warnings.IsEmpty)
			{
				throw new InvalidDataException("Исправьте предупреждения формата в существующем файле перед добавлением.");
			}

			var separator = string.IsNullOrEmpty(source.TrimStart('\uFEFF'))
				? string.Empty
				: "\r\n\r\n";

			var addition = Utf8.GetBytes(separator + block + "\r\n");
			temporaryPath = Path.Combine(directory, $".{Path.GetFileName(SourcePath)}.{Guid.NewGuid():N}.tmp");
			await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
			{
				await stream.WriteAsync(bytes, cancellationToken);
				await stream.WriteAsync(addition, cancellationToken);
				await stream.FlushAsync(cancellationToken);
				stream.Flush(flushToDisk: true);
			}

			cancellationToken.ThrowIfCancellationRequested();
			var currentBytes = File.Exists(SourcePath)
				? await File.ReadAllBytesAsync(SourcePath, cancellationToken)
				: [];

			if (!bytes.AsSpan().SequenceEqual(currentBytes))
			{
				throw new IOException("Файл изменился во время записи. Повторите добавление.");
			}

			File.Move(temporaryPath, SourcePath, overwrite: true);
			temporaryPath = null;
			PublishSnapshot(source + separator + block + "\r\n");
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DecoderFallbackException or InvalidDataException)
		{
			_logger.LogError(exception, "Не удалось добавить вопрос в {SourcePath}.", SourcePath);
			var message = exception is InvalidDataException
				? exception.Message
				: GetFileError(exception);
			throw new CheatSheetStorageException(message, exception);
		}
		finally
		{
			DeleteTemporaryFile(temporaryPath);
			_fileLock.Release();
		}
	}

	/// <summary>
	/// Освобождает барьер операций при завершении сервера.
	/// </summary>
	public void Dispose()
	{
		_fileLock.Dispose();
	}

	/// <summary>
	/// Публикует результат чтения одной атомарной заменой ссылки.
	/// </summary>
	/// <param name="source"> Декодированное содержимое файла для построения нового снимка. </param>
	private void PublishSnapshot(string source)
	{
		var result = CheatSheetParser.Parse(source, _options.ReservedAnswerMarkers);
		var snapshot = new CheatSheetSnapshot(result.Questions, SourcePath, DateTimeOffset.UtcNow, null, result.Warnings);
		Volatile.Write(ref _snapshot, snapshot);
	}

	/// <summary>
	/// Удаляет временный файл текущей операции, не маскируя исходную ошибку.
	/// </summary>
	/// <param name="path"> Путь к временному файлу либо null, если очистка не требуется. </param>
	private void DeleteTemporaryFile(string? path)
	{
		if (path is null)
		{
			return;
		}
		try
		{
			File.Delete(path);
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
		{
			_logger.LogWarning(exception, "Не удалось удалить временный файл {Path}.", path);
		}
	}

	/// <summary>
	/// Преобразует файловые ошибки в сообщение без внутренних путей и стека вызовов.
	/// </summary>
	/// <param name="exception"> Ошибка файловой системы или декодирования UTF-8. </param>
	private static string GetFileError(Exception exception)
	{
		return exception switch
		{
			FileNotFoundException or DirectoryNotFoundException =>
				"Файл ответов не найден. Поместите его в каталог приложения или добавьте первый вопрос.",
			UnauthorizedAccessException => "Нет доступа к файлу ответов. Проверьте права на файл и его каталог.",
			DecoderFallbackException => "Файл должен быть сохранён в кодировке UTF-8.",
			IOException => "Файл недоступен или изменился во время записи. Закройте редактор и повторите операцию.",
			_ => throw new ArgumentOutOfRangeException(nameof(exception), exception, "Неизвестная ошибка хранения.")
		};
	}
}
