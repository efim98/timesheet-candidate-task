using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TimeSheetCandidateTask.Domain.Models;
using TimesheetCandidateTask.Api.Application;
using TimesheetCandidateTask.Api.Infrastructure;
using TimesheetCandidateTask.Shared.Contracts;
using Xunit;
using Xunit.Abstractions;

namespace TimesheetCandidateTask.Tests;

/// <summary>
/// Проверяет серверную логику API: создание табеля, обновление комментариев, валидацию дат и запрет изменений после утверждения.
/// </summary>
public sealed class TimesheetApiUnitTests
{
    private static readonly DateTime PeriodStart = new(2026, 1, 1);
    private readonly ITestOutputHelper _output;

    public TimesheetApiUnitTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Проверяет создание табеля и корректный расчёт итогов и комментариев.
    /// </summary>
    [Fact]
    public async Task Save_creates_timesheet_and_returns_totals_and_comments()
    {
        LogScenario();
        await using var fixture = await ApiFixture.StartAsync();
        var request = CreateSaveRequest(fixture.RetailId, new[] { NewLine() });
        _output.WriteLine($"Входные данные SaveAsync: {Describe(request)}");

        var result = await fixture.Service.SaveAsync(request, CancellationToken.None);

        var line = Assert.Single(result.Lines);
        Assert.Equal(TimesheetStatus.Draft, result.Status);
        Assert.Equal(15, line.Totals.TotalHours);
        Assert.Equal("Первый день", line.Days.Single(day => day.Date == PeriodStart).Comment);
        Assert.Equal("Второй день", line.Days.Single(day => day.Date == PeriodStart.AddDays(1)).Comment);
        _output.WriteLine(
            $"Результат: создан табель id={result.Id}, строка id={line.Id}, " +
            $"итого={line.Totals.TotalHours}, дни={line.Days.Count}.");
    }

    /// <summary>
    /// Проверяет обновление значений дня и сохранение идентификаторов сущностей.
    /// </summary>
    [Fact]
    public async Task Save_updates_existing_day_values_and_preserves_entity_ids()
    {
        LogScenario();
        await using var fixture = await ApiFixture.StartAsync();
        var initial = await fixture.Service.SaveAsync(
            CreateSaveRequest(fixture.RetailId, new[] { NewLine() }),
            CancellationToken.None);
        var initialLine = Assert.Single(initial.Lines);
        var initialDay = initialLine.Days.First(day => day.Date == PeriodStart);
        var databaseDayId = await fixture.Db.TimesheetDays
            .Where(day => day.TimesheetLineId == initialLine.Id && day.Date == PeriodStart)
            .Select(day => day.Id)
            .SingleAsync();
        var update = CreateSaveRequest(
            fixture.RetailId,
            new[] { NewLine(hours: 6, firstComment: "Исправлено") });
        _output.WriteLine(
            $"Входные данные: lineId={initialLine.Id}, dayId={databaseDayId}, " +
            "новые часы=6, комментарий='Исправлено'.");

        var result = await fixture.Service.SaveAsync(update, CancellationToken.None);

        var updatedLine = Assert.Single(result.Lines);
        var updatedDay = updatedLine.Days.Single(day => day.Date == PeriodStart);
        var updatedDatabaseDayId = await fixture.Db.TimesheetDays
            .Where(day => day.TimesheetLineId == updatedLine.Id && day.Date == PeriodStart)
            .Select(day => day.Id)
            .SingleAsync();
        Assert.Equal(initialLine.Id, updatedLine.Id);
        Assert.Equal(initialDay.Date, updatedDay.Date);
        Assert.Equal(databaseDayId, updatedDatabaseDayId);
        Assert.Equal(6, updatedDay.Hours);
        Assert.Equal("Исправлено", updatedDay.Comment);
        _output.WriteLine(
            $"Результат: сохранены часы={updatedDay.Hours} и комментарий='{updatedDay.Comment}', " +
            $"ID строки/дня остались {updatedLine.Id}/{updatedDatabaseDayId}.");
    }

    /// <summary>
    /// Проверяет, что отсутствующий табель возвращает null, а существующий загружается корректно.
    /// </summary>
    [Fact]
    public async Task Get_returns_null_for_missing_timesheet_and_loaded_model_for_existing()
    {
        LogScenario();
        await using var fixture = await ApiFixture.StartAsync();
        var missingRetailId = Guid.NewGuid();
        _output.WriteLine($"Входные данные: retailId отсутствующего табеля={missingRetailId}.");

        var missing = await fixture.Service.GetAsync(missingRetailId, PeriodStart, CancellationToken.None);
        await fixture.Service.SaveAsync(
            CreateSaveRequest(fixture.RetailId, new[] { NewLine() }),
            CancellationToken.None);
        var existing = await fixture.Service.GetAsync(fixture.RetailId, PeriodStart, CancellationToken.None);

        Assert.Null(missing);
        Assert.NotNull(existing);
        Assert.Single(existing!.Lines);
        _output.WriteLine("Результат: отсутствующий табель -> null; существующий табель и его строка загружены.");
    }

    /// <summary>
    /// Проверяет валидацию периода, запрет повторяющихся сотрудников и неверных дат внутри периода.
    /// </summary>
    [Fact]
    public async Task Save_rejects_invalid_period_duplicate_employees_and_invalid_day_dates()
    {
        LogScenario();
        await using var fixture = await ApiFixture.StartAsync();
        var validLine = NewLine();

        _output.WriteLine("Входные данные: начало периода 2026-01-02.");
        var invalidPeriod = await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.SaveAsync(
            CreateSaveRequest(fixture.RetailId, new[] { validLine }, new DateTime(2026, 1, 2)),
            CancellationToken.None));
        _output.WriteLine($"Результат: {invalidPeriod.Message}");

        _output.WriteLine("Входные данные: две строки с одинаковым EmployeeId.");
        var duplicateEmployee = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.SaveAsync(
            CreateSaveRequest(fixture.RetailId, new[] { validLine, validLine }),
            CancellationToken.None));
        _output.WriteLine($"Результат: {duplicateEmployee.Message}");

        _output.WriteLine("Входные данные: день 2026-02-01 при периоде 2026-01-01.");
        var outsideMonth = NewLine(days: new[]
        {
            NewDay(new DateTime(2026, 2, 1), 8, "За пределами периода")
        });
        var invalidDay = await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.SaveAsync(
            CreateSaveRequest(fixture.RetailId, new[] { outsideMonth }),
            CancellationToken.None));
        Assert.Empty(await fixture.Db.Timesheets.ToListAsync());
        _output.WriteLine($"Результат: {invalidDay.Message}; данные не добавлены.");
    }

    /// <summary>
    /// Проверяет запрет повторяющихся дат внутри одной строки сотрудника.
    /// </summary>
    [Fact]
    public async Task Save_rejects_duplicate_dates_in_a_line()
    {
        LogScenario();
        await using var fixture = await ApiFixture.StartAsync();
        var duplicateDateLine = NewLine(days: new[]
        {
            NewDay(PeriodStart, 8, "Первая запись"),
            NewDay(PeriodStart, 4, "Дубликат")
        });
        _output.WriteLine("Входные данные: две записи одного сотрудника за 2026-01-01.");

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.SaveAsync(
            CreateSaveRequest(fixture.RetailId, new[] { duplicateDateLine }),
            CancellationToken.None));

        Assert.Empty(await fixture.Db.Timesheets.ToListAsync());
        _output.WriteLine($"Результат: {exception.Message}; табель не создан.");
    }

    /// <summary>
    /// Проверяет создание новой строки сотрудника и запрет дублирования сотрудника в табеле.
    /// </summary>
    [Fact]
    public async Task Add_line_creates_unique_employee_line_and_rejects_duplicate_employee()
    {
        LogScenario();
        await using var fixture = await ApiFixture.StartAsync();
        await fixture.Service.SaveAsync(
            CreateSaveRequest(fixture.RetailId, new[] { NewLine() }),
            CancellationToken.None);
        var secondEmployeeId = Guid.NewGuid();
        var request = new AddTimesheetLineRequest(
            secondEmployeeId,
            Guid.NewGuid(),
            EmploymentType.PartTime,
            true,
            new[] { NewDay(PeriodStart, 7, "Новая строка") });
        _output.WriteLine($"Входные данные AddLineAsync: employeeId={secondEmployeeId}, days=1.");

        var added = await fixture.Service.AddLineAsync(
            fixture.RetailId, PeriodStart, request, CancellationToken.None);

        Assert.Equal(2, added.Lines.Count);
        Assert.Contains(added.Lines, line => line.EmployeeId == secondEmployeeId);
        _output.WriteLine($"Результат: в табеле {added.Lines.Count} строки.");

        var duplicateRequest = request with { EmployeeId = added.Lines.First().EmployeeId };
        _output.WriteLine($"Входные данные повторного AddLineAsync: employeeId={duplicateRequest.EmployeeId}.");
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.AddLineAsync(
                fixture.RetailId, PeriodStart, duplicateRequest, CancellationToken.None));
        Assert.Equal("Для этого сотрудника строка в табеле уже существует.", exception.Message);
        _output.WriteLine($"Результат: дубликат отклонён — {exception.Message}");
    }

    /// <summary>
    /// Проверяет обновление комментария только для выбранного дня и обработку отсутствующих сущностей.
    /// </summary>
    [Fact]
    public async Task Update_comment_changes_only_requested_day_and_returns_not_found_for_missing_entities()
    {
        LogScenario();
        await using var fixture = await ApiFixture.StartAsync();
        var created = await fixture.Service.SaveAsync(
            CreateSaveRequest(fixture.RetailId, new[] { NewLine() }),
            CancellationToken.None);
        var line = Assert.Single(created.Lines);
        var request = new UpdateDayCommentRequest(PeriodStart, "Изменённый комментарий");
        _output.WriteLine($"Входные данные UpdateDayCommentAsync: lineId={line.Id}, date={request.Date:d}, comment='{request.Comment}'.");

        var updated = await fixture.Service.UpdateDayCommentAsync(
            fixture.RetailId, PeriodStart, line.Id, request, CancellationToken.None);

        Assert.Equal("Изменённый комментарий", updated.Lines.Single().Days
            .Single(day => day.Date == PeriodStart).Comment);
        Assert.Equal("Второй день", updated.Lines.Single().Days
            .Single(day => day.Date == PeriodStart.AddDays(1)).Comment);
        _output.WriteLine("Результат: изменён только комментарий запрошенного дня.");

        var missingLine = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            fixture.Service.UpdateDayCommentAsync(
                fixture.RetailId,
                PeriodStart,
                -1,
                request,
                CancellationToken.None));
        var missingDay = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            fixture.Service.UpdateDayCommentAsync(
                fixture.RetailId,
                PeriodStart,
                line.Id,
                new UpdateDayCommentRequest(new DateTime(2026, 1, 4), "Нет такого дня"),
                CancellationToken.None));
        _output.WriteLine($"Результат для отсутствующих ресурсов: строка='{missingLine.Message}', день='{missingDay.Message}'.");
    }

    /// <summary>
    /// Проверяет запрет сохранения, добавления строки и изменения комментария для утверждённого табеля.
    /// </summary>
    [Fact]
    public async Task Mutations_are_rejected_for_approved_timesheet_without_changing_data()
    {
        LogScenario();
        await using var fixture = await ApiFixture.StartAsync();
        var created = await fixture.Service.SaveAsync(
            CreateSaveRequest(fixture.RetailId, new[] { NewLine() }),
            CancellationToken.None);
        var approved = await fixture.Service.SaveAsync(
            CreateSaveRequest(fixture.RetailId, new[] { NewLine() }, status: TimesheetStatus.Approved),
            CancellationToken.None);
        var lineId = Assert.Single(approved.Lines).Id;
        _output.WriteLine($"Входные данные: утверждённый табель id={approved.Id}, status={approved.Status}.");

        var saveException = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.SaveAsync(
            CreateSaveRequest(fixture.RetailId, new[] { NewLine(hours: 3) }),
            CancellationToken.None));
        var addException = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.AddLineAsync(
            fixture.RetailId,
            PeriodStart,
            new AddTimesheetLineRequest(Guid.NewGuid(), Guid.NewGuid(), EmploymentType.Main, false, Array.Empty<SaveTimesheetDayRequest>()),
            CancellationToken.None));
        var commentException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.UpdateDayCommentAsync(
                fixture.RetailId,
                PeriodStart,
                lineId,
                new UpdateDayCommentRequest(PeriodStart, "Не должен сохраниться"),
                CancellationToken.None));

        Assert.Equal("Согласованный табель нельзя изменять.", saveException.Message);
        Assert.Equal(saveException.Message, addException.Message);
        Assert.Equal(saveException.Message, commentException.Message);
        var persisted = await fixture.Service.GetAsync(fixture.RetailId, PeriodStart, CancellationToken.None);
        Assert.Equal(TimesheetStatus.Approved, persisted!.Status);
        Assert.Equal(8, Assert.Single(persisted.Lines).Days.Single(day => day.Date == PeriodStart).Hours);
        Assert.Equal("Первый день", persisted.Lines.Single().Days.Single(day => day.Date == PeriodStart).Comment);
        Assert.NotEqual(0, created.Id);
        _output.WriteLine(
            $"Результат: Save/AddLine/UpdateComment отклонены одинаковой ошибкой; " +
            $"status={persisted.Status}, исходные часы и комментарий не изменились.");
    }

    private void LogScenario([System.Runtime.CompilerServices.CallerMemberName] string testName = "") =>
        _output.WriteLine($"=== API unit: {testName} ===");

    private static SaveTimesheetRequest CreateSaveRequest(
        Guid retailId,
        IReadOnlyCollection<SaveTimesheetLineRequest> lines,
        DateTime? periodStart = null,
        TimesheetStatus status = TimesheetStatus.Draft) =>
        new(retailId, periodStart ?? PeriodStart, status, lines);

    private static SaveTimesheetLineRequest NewLine(
        decimal hours = 8,
        string firstComment = "Первый день",
        IReadOnlyCollection<SaveTimesheetDayRequest>? days = null) =>
        new(
            Guid.Parse("e8abb9fa-b564-41ae-8d1f-fbbc99c0f54e"),
            Guid.NewGuid(),
            EmploymentType.Main,
            false,
            days ?? new[]
            {
                NewDay(PeriodStart, hours, firstComment),
                NewDay(PeriodStart.AddDays(1), 7, "Второй день")
            });

    private static SaveTimesheetDayRequest NewDay(DateTime date, decimal hours, string? comment) =>
        new(date, hours, TimesheetDayType.Workday, null, comment);

    private static string Describe(SaveTimesheetRequest request) =>
        $"retailId={request.RetailId}, period={request.PeriodStart:yyyy-MM-dd}, status={request.Status}, " +
        $"lines={request.Lines.Count}, days={request.Lines.Sum(line => line.Days.Count)}.";

    private sealed class ApiFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private ApiFixture(SqliteConnection connection, TimesheetDbContext db, Guid retailId)
        {
            _connection = connection;
            Db = db;
            RetailId = retailId;
            Service = new TimesheetService(db);
        }

        public TimesheetDbContext Db { get; }
        public Guid RetailId { get; }
        public TimesheetService Service { get; }

        public static async Task<ApiFixture> StartAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<TimesheetDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new TimesheetDbContext(options);
            await db.Database.EnsureCreatedAsync();
            return new ApiFixture(connection, db, Guid.NewGuid());
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
