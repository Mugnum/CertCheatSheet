using System.Text;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Mugnum.CertCheatSheet.Configuration;
using Mugnum.CertCheatSheet.Models;
using Mugnum.CertCheatSheet.Storage;

namespace Mugnum.CertCheatSheet.Tests;

/// <summary>
/// Проверки реальной файловой системы в отдельном временном каталоге.
/// </summary>
public sealed class StoreTests : IDisposable
{
	/// <summary>
	/// Каталог, созданный исключительно для этого экземпляра теста.
	/// </summary>
	private readonly string _directory = Path.Combine(Path.GetTempPath(), $"CertCheatSheet.Tests.{Guid.NewGuid():N}");

	/// <summary>
	/// Создаёт изолированный корень файла ответов.
	/// </summary>
	public StoreTests()
	{
		Directory.CreateDirectory(_directory);
	}

	/// <summary>
	/// Относительный путь использует корень приложения, а абсолютный остаётся абсолютным.
	/// </summary>
	[Fact]
	public void ResolveSourcePathFromApplicationRoot()
	{
		using var relative = CreateStore("data/answers.txt");
		using var absolute = CreateStore(Path.Combine(_directory, "absolute.txt"));
		Assert.Equal(Path.Combine(_directory, "data", "answers.txt"), relative.SourcePath);
		Assert.Equal(Path.Combine(_directory, "absolute.txt"), absolute.SourcePath);
	}

	/// <summary>
	/// Отсутствие файла даёт диагностируемое состояние без остановки сервера.
	/// </summary>
	[Fact]
	public async Task ReloadMissingFileKeepsEmptySnapshot()
	{
		using var store = CreateStore();
		await store.ReloadAsync();
		Assert.Empty(store.Snapshot.Questions);
		Assert.Contains("не найден", store.Snapshot.Error);
		Assert.Null(store.Snapshot.LoadedAt);
	}

	/// <summary>
	/// Ошибка кодировки сохраняет предыдущую успешную версию и время её загрузки.
	/// </summary>
	[Fact]
	public async Task ReloadInvalidUtf8PreservesLoadedQuestions()
	{
		using var store = CreateStore();
		await File.WriteAllTextAsync(store.SourcePath, "# Вопрос\n+ Ответ", Encoding.UTF8);
		await store.ReloadAsync();
		var snapshot = store.Snapshot;
		await File.WriteAllBytesAsync(store.SourcePath, [0xff, 0xfe, 0x80]);
		await store.ReloadAsync();

		Assert.Equal(snapshot.Questions, store.Snapshot.Questions);
		Assert.Equal(snapshot.LoadedAt, store.Snapshot.LoadedAt);
		Assert.Contains("UTF-8", store.Snapshot.Error);
	}

	/// <summary>
	/// Параллельные сохранения не теряют вопросы, а исходные байты и BOM сохраняются.
	/// </summary>
	[Fact]
	public async Task AddConcurrentQuestionsPreservesOriginalBytes()
	{
		using var store = CreateStore();
		var original = Encoding.UTF8.GetPreamble().Concat([.. "# Исходный\n- Ответ\n"u8]).ToArray();
		await File.WriteAllBytesAsync(store.SourcePath, original);
		var writes = Enumerable.Range(1, 12)
			.Select(number => store.AddQuestionAsync(new CheatSheetQuestion($"Новый {number}",
				[new CheatSheetAnswer('-', "Другой"), new CheatSheetAnswer('+', "Правильный")])));
		await Task.WhenAll(writes);
		var saved = await File.ReadAllBytesAsync(store.SourcePath);

		Assert.True(saved.AsSpan().StartsWith(original));
		Assert.Equal(13, store.Snapshot.Questions.Length);
		Assert.Empty(store.Snapshot.Warnings);
		Assert.All(store.Snapshot.Questions.Skip(1), question => Assert.Equal('+', question.Answers[0].Marker));
		Assert.Empty(Directory.GetFiles(_directory, ".*.tmp"));
	}

	/// <summary>
	/// Форма может создать отсутствующий файл в относительном вложенном каталоге.
	/// </summary>
	[Fact]
	public async Task AddCreatesMissingFile()
	{
		using var store = CreateStore("nested/new.txt");
		await store.AddQuestionAsync(new CheatSheetQuestion("Новый", [new CheatSheetAnswer('-', "Не подтверждён")]));
		Assert.True(File.Exists(store.SourcePath));
		Assert.Single(store.Snapshot.Questions);
		Assert.False(store.Snapshot.Questions[0].HasKnownCorrectAnswer);
		Assert.Null(store.Snapshot.Error);
	}

	/// <summary>
	/// Сохраняет разделитель даже при исходном файле из пробелов без перевода строки.
	/// </summary>
	[Fact]
	public async Task AddToWhitespaceFileProducesReadableBlock()
	{
		using var store = CreateStore();
		await File.WriteAllTextAsync(store.SourcePath, "   ", new UTF8Encoding(false));
		await store.AddQuestionAsync(new CheatSheetQuestion("Новый", [new CheatSheetAnswer('+', "Да")]));
		Assert.Single(store.Snapshot.Questions);
		Assert.Empty(store.Snapshot.Warnings);
	}

	/// <summary>
	/// Нарушения формата блокируют добавление без изменения существующего содержимого.
	/// </summary>
	[Fact]
	public async Task AddRejectsMalformedSourceWithoutOverwriting()
	{
		using var store = CreateStore();
		await File.WriteAllTextAsync(store.SourcePath, "Повреждённый блок\n- Ответ", Encoding.UTF8);
		var original = await File.ReadAllBytesAsync(store.SourcePath);

		await Assert.ThrowsAsync<CheatSheetStorageException>(() =>
			store.AddQuestionAsync(new CheatSheetQuestion("Новый", [new CheatSheetAnswer('+', "Да")])));

		Assert.Equal(original, await File.ReadAllBytesAsync(store.SourcePath));
		Assert.Empty(Directory.GetFiles(_directory, ".*.tmp"));
	}

	/// <summary>
	/// Запрет редактирования проверяется в хранилище, независимо от страницы.
	/// </summary>
	[Fact]
	public async Task AddRespectsDisabledEditing()
	{
		using var store = CreateStore(enableEditing: false);
		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			store.AddQuestionAsync(new CheatSheetQuestion("Новый", [new CheatSheetAnswer('+', "Да")])));
		Assert.False(File.Exists(store.SourcePath));
	}

	/// <summary>
	/// Занятый сторонним процессом файл не заменяется после отказа чтения.
	/// </summary>
	[Fact]
	public async Task AddToLockedFileDoesNotChangeContent()
	{
		using var store = CreateStore();
		const string Original = "# Вопрос\n+ Ответ";
		await File.WriteAllTextAsync(store.SourcePath, Original, new UTF8Encoding(false));

		await using (var lease = File.Open(store.SourcePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
		{
			await Assert.ThrowsAsync<CheatSheetStorageException>(() =>
				store.AddQuestionAsync(new CheatSheetQuestion("Новый", [new CheatSheetAnswer('+', "Да")])));
		}

		Assert.Equal(Original, await File.ReadAllTextAsync(store.SourcePath));
		Assert.Empty(Directory.GetFiles(_directory, ".*.tmp"));
	}

	/// <summary>
	/// Отменённая операция не создаёт источник и не удерживает барьер для следующей записи.
	/// </summary>
	[Fact]
	public async Task AddCancellationDoesNotWriteAndReleasesLock()
	{
		using var store = CreateStore();
		using var cancellation = new CancellationTokenSource();
		await cancellation.CancelAsync();
		var question = new CheatSheetQuestion("Новый", [new CheatSheetAnswer('+', "Да")]);
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.AddQuestionAsync(question, cancellation.Token));
		Assert.False(File.Exists(store.SourcePath));
		await store.AddQuestionAsync(question);
		Assert.Single(store.Snapshot.Questions);
	}

	/// <summary>
	/// Удаляет только созданный этим тестом каталог внутри системного временного каталога.
	/// </summary>
	public void Dispose()
	{
		var path = Path.GetFullPath(_directory);
		var temporaryRoot = Path.GetFullPath(Path.GetTempPath());

		if (!path.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase)
			|| !Path.GetFileName(path).StartsWith("CertCheatSheet.Tests.", StringComparison.Ordinal))
		{
			throw new InvalidOperationException("Отказ от удаления каталога вне временного корня тестов.");
		}

		Directory.Delete(path, recursive: true);
	}

	/// <summary>
	/// Создаёт хранилище с явным корнем, не изменяя текущий каталог процесса.
	/// </summary>
	/// <param name="filePath"> Путь к источнику относительно временного корня либо абсолютный путь. </param>
	/// <param name="enableEditing"> Разрешение записи новых вопросов в тестовое хранилище. </param>
	private CheatSheetStore CreateStore(string filePath = "answers.txt", bool enableEditing = true)
	{
		var options = Options.Create(new CheatSheetOptions
		{
			FilePath = filePath,
			EnableEditing = enableEditing
		});
		var environment = new TestEnvironment { ContentRootPath = _directory };
		return new CheatSheetStore(options, environment, NullLogger<CheatSheetStore>.Instance);
	}

	/// <summary>
	/// Минимальная среда приложения для файловых проверок.
	/// </summary>
	private sealed class TestEnvironment : IHostEnvironment
	{
		/// <inheritdoc />
		public string EnvironmentName { get; set; } = Environments.Production;

		/// <inheritdoc />
		public string ApplicationName { get; set; } = "CertCheatSheet.Tests";

		/// <inheritdoc />
		public string ContentRootPath { get; set; } = string.Empty;

		/// <inheritdoc />
		public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
	}
}
