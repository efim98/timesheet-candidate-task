using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TimeSheetCandidateTask.Domain.Models;
using TimesheetCandidateTask.Api.Application;
using TimesheetCandidateTask.Api.Controllers;
using TimesheetCandidateTask.Api.Infrastructure;
using TimesheetCandidateTask.Shared;
using TimesheetCandidateTask.Shared.Contracts;
using TimesheetCandidateTask.Wpf.Application;
using TimesheetCandidateTask.Wpf.Infrastructure;
using TimesheetCandidateTask.Wpf.ViewModels;
using Xunit;
using Xunit.Abstractions;

namespace TimesheetCandidateTask.Tests;

/// <summary>
/// Проверяет интеграцию ViewModel с реальным HTTP API: загрузку, сохранение комментариев, создание строк и запрет изменений после утверждения.
/// </summary>
public sealed class TimesheetViewModelApiIntegrationTests
{
    private static readonly DateTime PeriodStart = new(2026, 1, 1);
    private readonly ITestOutputHelper _output;

    public TimesheetViewModelApiIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Проверяет загрузку существующего черновика и включение команд редактирования.
    /// </summary>
    [Fact]
    public async Task Load_command_loads_existing_draft_and_enables_editing_commands()
    {
        LogScenario();
        await using var fixture = await StartFixtureAsync();
        var viewModel = fixture.CreateViewModel();

        _output.WriteLine("Проверяю, что до загрузки команды изменения выключены.");
        Assert.False(viewModel.SaveCommand.CanExecute(null));
        Assert.False(viewModel.AddEmployeeCommand.CanExecute(null));
        Assert.False(viewModel.ApproveCommand.CanExecute(null));

        _output.WriteLine("Загружаю черновик командой ViewModel через настоящий HTTP API.");
        viewModel.LoadCommand.Execute(null);

        _output.WriteLine("Проверяю статус, комментарии и доступность команд редактирования.");
        Assert.True(viewModel.IsLoaded);
        Assert.Equal("Табель загружен.", viewModel.Message);
        Assert.Equal("Черновик", viewModel.StatusText);
        Assert.Single(viewModel.Lines);
        Assert.True(viewModel.SaveCommand.CanExecute(null));
        Assert.True(viewModel.AddEmployeeCommand.CanExecute(null));
        Assert.True(viewModel.ApproveCommand.CanExecute(null));
        Assert.False(viewModel.AreCommentsReadOnly);
        Assert.Equal("Инвентаризация", Assert.Single(viewModel.Lines).FirstDayComment);
    }

    /// <summary>
    /// Проверяет сохранение обоих комментариев через HTTP API.
    /// </summary>
    [Fact]
    public async Task Save_command_persists_both_comments_through_http_api()
    {
        LogScenario();
        await using var fixture = await StartFixtureAsync();
        var viewModel = fixture.CreateViewModel();
        viewModel.LoadCommand.Execute(null);

        var line = Assert.Single(viewModel.Lines);
        _output.WriteLine("Изменяю комментарии для 1 и 2 января и сохраняю через ViewModel.");
        _output.WriteLine(
            $"Входные данные: lineId={line.Line.Id}; 2026-01-01='Обновлённый комментарий за 1 января'; " +
            "2026-01-02='Комментарий за 2 января'.");
        line.FirstDayComment = "Обновлённый комментарий за 1 января";
        line.SecondDayComment = "Комментарий за 2 января";
        Assert.Equal("Есть несохранённые изменения комментариев.", viewModel.Message);

        viewModel.SaveCommand.Execute(null);

        Assert.Equal("Изменения сохранены.", viewModel.Message);
        Assert.False(Assert.Single(viewModel.Lines).IsDirty);
        _output.WriteLine("Повторно читаю табель по HTTP и сверяю сохранённые комментарии.");
        var persisted = await fixture.GetTimesheetAsync();
        var days = Assert.Single(persisted.Lines).Days.ToDictionary(day => day.Date.Date);
        Assert.Equal("Обновлённый комментарий за 1 января", days[new DateTime(2026, 1, 1)].Comment);
        Assert.Equal("Комментарий за 2 января", days[new DateTime(2026, 1, 2)].Comment);
        _output.WriteLine($"Результат: табель {persisted.Id}, статус={persisted.Status}, оба комментария сохранены.");
    }

    /// <summary>
    /// Проверяет очистку комментария и его сохранение в API.
    /// </summary>
    [Fact]
    public async Task Save_command_can_clear_a_comment()
    {
        LogScenario();
        await using var fixture = await StartFixtureAsync();
        var viewModel = fixture.CreateViewModel();
        viewModel.LoadCommand.Execute(null);

        _output.WriteLine("Очищаю комментарий, сохраняю и проверяю значение в API.");
        _output.WriteLine("Входные данные: комментарий строки за 2026-01-01 = пустая строка.");
        Assert.Single(viewModel.Lines).FirstDayComment = string.Empty;
        viewModel.SaveCommand.Execute(null);

        var persisted = await fixture.GetTimesheetAsync();
        Assert.Equal(string.Empty, Assert.Single(persisted.Lines).Days
            .Single(day => day.Date.Date == PeriodStart).Comment);
        _output.WriteLine($"Результат: табель {persisted.Id}, комментарий за 2026-01-01 очищен.");
    }

    /// <summary>
    /// Проверяет добавление сотрудника и сохранение новой строки через HTTP API.
    /// </summary>
    [Fact]
    public async Task Add_employee_and_save_persist_new_line_through_http_api()
    {
        LogScenario();
        await using var fixture = await StartFixtureAsync();
        var viewModel = fixture.CreateViewModel();
        viewModel.LoadCommand.Execute(null);

        _output.WriteLine("Добавляю сотрудника и сохраняю строку через команду ViewModel.");
        viewModel.AddEmployeeCommand.Execute(null);
        var existingLineId = viewModel.Lines.First().Line.Id;
        var newLine = viewModel.Lines.Last();
        var temporaryLineId = newLine.Line.Id;
        _output.WriteLine(
            $"Входные данные: employeeId={newLine.Line.EmployeeId}; temporaryLineId={temporaryLineId}; " +
            $"строк после добавления={viewModel.Lines.Count}.");
        Assert.Equal(2, viewModel.Lines.Count);
        Assert.True(newLine.IsNew);
        Assert.True(temporaryLineId < 0);
        viewModel.SaveCommand.Execute(null);

        Assert.Equal("Изменения сохранены.", viewModel.Message);
        Assert.Equal(2, viewModel.Lines.Count);
        Assert.All(viewModel.Lines, line => Assert.False(line.IsNew));
        var persisted = await fixture.GetTimesheetAsync();
        Assert.Equal(2, persisted.Lines.Count);
        var persistedExistingLine = persisted.Lines.Single(line => line.EmployeeId == viewModel.Lines.First().Line.EmployeeId);
        var persistedNewLine = persisted.Lines.Single(line => line.EmployeeId == newLine.Line.EmployeeId);
        Assert.Equal(existingLineId, persistedExistingLine.Id);
        Assert.True(persistedNewLine.Id > 0);
        Assert.NotEqual(existingLineId, persistedNewLine.Id);
        _output.WriteLine(
            $"Результат: POST создал новую строку с ID БД={persistedNewLine.Id}; " +
            $"существующая строка сохранила ID={persistedExistingLine.Id}.");
    }

    /// <summary>
    /// Проверяет передачу комментария новой строки при создании записи.
    /// </summary>
    [Fact]
    public async Task Comment_changed_on_new_line_is_included_when_line_is_created()
    {
        LogScenario();
        await using var fixture = await StartFixtureAsync();
        var viewModel = fixture.CreateViewModel();
        viewModel.LoadCommand.Execute(null);
        _output.WriteLine("Добавляю строку, редактирую её комментарий и отправляю строку в API.");
        viewModel.AddEmployeeCommand.Execute(null);
        var newLine = viewModel.Lines.Last();
        _output.WriteLine(
            $"Входные данные: employeeId={newLine.Line.EmployeeId}; " +
            "комментарий за 2026-01-01='Комментарий новой строки'.");
        newLine.FirstDayComment = "Комментарий новой строки";

        viewModel.SaveCommand.Execute(null);

        var persisted = await fixture.GetTimesheetAsync();
        Assert.Equal(
            "Комментарий новой строки",
            persisted.Lines.Single(line => line.EmployeeId == newLine.Line.EmployeeId)
                .Days.Single(day => day.Date.Date == PeriodStart).Comment);
        _output.WriteLine("Результат: новая строка и её комментарий сохранены API.");
    }

    /// <summary>
    /// Проверяет добавление всех доступных сотрудников и сохранение всех строк.
    /// </summary>
    [Fact]
    public async Task Adding_all_available_employees_saves_each_line_and_disables_add_command()
    {
        LogScenario();
        await using var fixture = await StartFixtureAsync();
        var viewModel = fixture.CreateViewModel();
        viewModel.LoadCommand.Execute(null);

        _output.WriteLine("Добавляю всех доступных сотрудников и сохраняю созданные строки.");
        while (viewModel.AddEmployeeCommand.CanExecute(null))
        {
            viewModel.AddEmployeeCommand.Execute(null);
        }

        Assert.Equal(EmployeesCollection.Employees.Count, viewModel.Lines.Count);
        Assert.False(viewModel.AddEmployeeCommand.CanExecute(null));
        viewModel.SaveCommand.Execute(null);

        var persisted = await fixture.GetTimesheetAsync();
        Assert.Equal(EmployeesCollection.Employees.Count, persisted.Lines.Count);
        Assert.Equal(
            persisted.Lines.Count,
            persisted.Lines.Select(line => line.EmployeeId).Distinct().Count());
        _output.WriteLine(
            $"Результат: API содержит {persisted.Lines.Count} уникальных строк, добавление отключено.");
    }

    /// <summary>
    /// Проверяет создание нового табеля после загрузки с 404 из API.
    /// </summary>
    [Fact]
    public async Task Saving_new_timesheet_after_load_404_creates_it_in_api()
    {
        LogScenario();
        var retailId = Guid.Parse("c4335485-a459-424f-90fc-8e8e4445f120");
        await using var fixture = await StartFixtureAsync(
            status: TimesheetStatus.Approved,
            retailId: retailId);
        var viewModel = fixture.CreateViewModel();

        _output.WriteLine(
            $"Загружаю табель для retailId={retailId} (API 404), рядом существует утверждённый табель другой точки.");
        viewModel.LoadCommand.Execute(null);

        Assert.True(viewModel.IsLoaded);
        Assert.Equal("Табель загружен.", viewModel.Message);
        Assert.Single(viewModel.Lines);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.GetRawTimesheetAsync()).StatusCode);

        viewModel.SaveCommand.Execute(null);

        Assert.Equal("Изменения сохранены.", viewModel.Message);
        var persisted = await fixture.GetTimesheetAsync();
        Assert.Equal(TimesheetStatus.Draft, persisted.Status);
        Assert.Equal(retailId, persisted.RetailId);
        Assert.Equal(
            TimesheetStatus.Approved,
            (await fixture.GetTimesheetAsync(TimesheetSeeder.RetailId)).Status);
        _output.WriteLine(
            $"Результат: создан табель id={persisted.Id} для retailId={persisted.RetailId}; " +
            "утверждённый табель другой точки не изменён.");
    }

    /// <summary>
    /// Проверяет утверждение табеля и перевод ViewModel в режим только для чтения.
    /// </summary>
    [Fact]
    public async Task Approve_command_persists_status_and_makes_view_model_read_only()
    {
        LogScenario();
        await using var fixture = await StartFixtureAsync();
        var viewModel = fixture.CreateViewModel();
        viewModel.LoadCommand.Execute(null);

        _output.WriteLine("Утверждаю табель через ViewModel и проверяю статус после повторного чтения API.");
        viewModel.ApproveCommand.Execute(null);

        Assert.Equal("Табель утверждён.", viewModel.Message);
        Assert.Equal("Утверждён", viewModel.StatusText);
        Assert.True(viewModel.AreCommentsReadOnly);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
        Assert.False(viewModel.AddEmployeeCommand.CanExecute(null));
        Assert.False(viewModel.ApproveCommand.CanExecute(null));
        var persisted = await fixture.GetTimesheetAsync();
        Assert.Equal(TimesheetStatus.Approved, persisted.Status);
        _output.WriteLine($"Результат: табель id={persisted.Id}, status={persisted.Status}; ViewModel read-only.");
    }

    /// <summary>
    /// Проверяет загрузку уже утверждённого табеля и блокировку команд изменения.
    /// </summary>
    [Fact]
    public async Task Loading_approved_timesheet_displays_read_only_message_and_disables_mutations()
    {
        LogScenario();
        await using var fixture = await StartFixtureAsync(status: TimesheetStatus.Approved);
        var viewModel = fixture.CreateViewModel();

        _output.WriteLine("Загружаю уже утверждённый табель и проверяю блокировку команд изменения.");
        viewModel.LoadCommand.Execute(null);

        Assert.Equal("Табель утверждён и доступен только для чтения.", viewModel.Message);
        Assert.True(viewModel.AreCommentsReadOnly);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
        Assert.False(viewModel.AddEmployeeCommand.CanExecute(null));
        Assert.False(viewModel.ApproveCommand.CanExecute(null));
        _output.WriteLine("Результат: команды сохранения, добавления и утверждения отключены.");
    }

    /// <summary>
    /// Проверяет обработку конфликта при сохранении после утверждения табеля другим клиентом.
    /// </summary>
    [Fact]
    public async Task Save_after_another_client_approves_timesheet_surfaces_conflict_and_preserves_data()
    {
        LogScenario();
        await using var fixture = await StartFixtureAsync();
        var viewModel = fixture.CreateViewModel();
        viewModel.LoadCommand.Execute(null);
        var loaded = await fixture.GetTimesheetAsync();
        var approveRequest = fixture.CreateSaveRequest(loaded, TimesheetStatus.Approved);
        _output.WriteLine("Другой клиент утверждает табель; затем текущая ViewModel пробует сохранить старую копию.");
        var approveResponse = await fixture.Client.PutAsJsonAsync("api/timesheets", approveRequest);
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        Assert.Single(viewModel.Lines).FirstDayComment = "Must not be saved";
        viewModel.SaveCommand.Execute(null);

        Assert.Equal("Согласованный табель нельзя изменять.", viewModel.Message);
        var persisted = await fixture.GetTimesheetAsync();
        Assert.Equal(TimesheetStatus.Approved, persisted.Status);
        Assert.Equal("Инвентаризация", Assert.Single(persisted.Lines).Days
            .Single(day => day.Date.Date == PeriodStart).Comment);
        _output.WriteLine($"Результат: API сохранил status={persisted.Status} и исходный комментарий.");
    }

    /// <summary>
    /// Проверяет ответы API 409 для сохранения, добавления строки и изменения комментария после утверждения.
    /// </summary>
    [Fact]
    public async Task Api_rejects_save_add_line_and_comment_update_after_approval()
    {
        LogScenario();
        await using var fixture = await StartFixtureAsync();
        var timesheet = await fixture.GetTimesheetAsync();
        var line = Assert.Single(timesheet.Lines);
        var approvalResponse = await fixture.Client.PutAsJsonAsync(
            "api/timesheets",
            fixture.CreateSaveRequest(timesheet, TimesheetStatus.Approved));
        Assert.Equal(HttpStatusCode.OK, approvalResponse.StatusCode);

        _output.WriteLine("Проверяю ответы API 409 для сохранения, добавления строки и изменения комментария.");
        var saveResponse = await fixture.Client.PutAsJsonAsync(
            "api/timesheets",
            fixture.CreateSaveRequest(timesheet, TimesheetStatus.Draft));
        var addResponse = await fixture.Client.PostAsJsonAsync(
            fixture.LinesUri,
            new AddTimesheetLineRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                EmploymentType.Main,
                false,
                Array.Empty<SaveTimesheetDayRequest>()));
        var updateCommentResponse = await fixture.Client.PutAsJsonAsync(
            $"{fixture.LinesUri}/{line.Id}/comment",
            new UpdateDayCommentRequest(PeriodStart, "Must not be saved"));

        await AssertConflictAsync(saveResponse);
        await AssertConflictAsync(addResponse);
        await AssertConflictAsync(updateCommentResponse);

        var persisted = await fixture.GetTimesheetAsync();
        Assert.Equal(TimesheetStatus.Approved, persisted.Status);
        Assert.Equal("Инвентаризация", Assert.Single(persisted.Lines).Days
            .Single(day => day.Date.Date == PeriodStart).Comment);
        _output.WriteLine($"Результат: все три изменения получили HTTP 409; данные табеля {persisted.Id} неизменны.");
    }

    /// <summary>
    /// Проверяет конфликт дубля сотрудника и HTTP 400 для ошибок периода.
    /// </summary>
    [Fact]
    public async Task Api_returns_conflict_for_duplicate_employees_and_bad_request_for_invalid_period()
    {
        LogScenario();
        await using var fixture = await StartFixtureAsync();
        var timesheet = await fixture.GetTimesheetAsync();
        _output.WriteLine("Отправляю дублирующую строку и некорректный период, проверяю HTTP 409 и 400.");
        var duplicateLine = fixture.CreateSaveRequest(timesheet, TimesheetStatus.Draft).Lines.Single();
        var duplicateResponse = await fixture.Client.PutAsJsonAsync(
            "api/timesheets",
            new SaveTimesheetRequest(
                TimesheetSeeder.RetailId,
                PeriodStart,
                TimesheetStatus.Draft,
                new[] { duplicateLine, duplicateLine }));
        var invalidPeriodResponse = await fixture.Client.GetAsync(
            $"api/timesheets?retailId={TimesheetSeeder.RetailId:D}&periodStart=2026-01-02");

        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidPeriodResponse.StatusCode);
        _output.WriteLine(
            $"Результат: дубликат -> HTTP {(int)duplicateResponse.StatusCode}; " +
            $"неверный период -> HTTP {(int)invalidPeriodResponse.StatusCode}.");
    }

    /// <summary>
    /// Проверяет конфликт дубля строки и HTTP 400 для дат вне периода.
    /// </summary>
    [Fact]
    public async Task Api_rejects_duplicate_line_and_dates_outside_the_period()
    {
        LogScenario();
        await using var fixture = await StartFixtureAsync();
        _output.WriteLine("Проверяю конфликт дубля сотрудника и 400 для дат вне месяца.");
        var timesheet = await fixture.GetTimesheetAsync();
        var existingLine = Assert.Single(timesheet.Lines);
        var duplicateLineResponse = await fixture.Client.PostAsJsonAsync(
            fixture.LinesUri,
            new AddTimesheetLineRequest(
                existingLine.EmployeeId,
                existingLine.PositionId,
                existingLine.EmploymentType,
                existingLine.IsNight,
                Array.Empty<SaveTimesheetDayRequest>()));
        var invalidLineDateResponse = await fixture.Client.PostAsJsonAsync(
            fixture.LinesUri,
            new AddTimesheetLineRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                EmploymentType.Main,
                false,
                new[]
                {
                    new SaveTimesheetDayRequest(
                        new DateTime(2026, 2, 1),
                        8,
                        TimesheetDayType.Workday,
                        null,
                        null)
                }));
        var invalidCommentDateResponse = await fixture.Client.PutAsJsonAsync(
            $"{fixture.LinesUri}/{existingLine.Id}/comment",
            new UpdateDayCommentRequest(new DateTime(2026, 2, 1), "Outside period"));

        Assert.Equal(HttpStatusCode.Conflict, duplicateLineResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidLineDateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidCommentDateResponse.StatusCode);
        _output.WriteLine(
            $"Результат: дублирующая строка -> HTTP {(int)duplicateLineResponse.StatusCode}; " +
            $"даты вне периода -> HTTP {(int)invalidLineDateResponse.StatusCode} и {(int)invalidCommentDateResponse.StatusCode}.");
    }

    /// <summary>
    /// Проверяет HTTP 404 для отсутствующей строки и дня табеля.
    /// </summary>
    [Fact]
    public async Task Api_returns_not_found_for_missing_timesheet_line_and_day()
    {
        LogScenario();
        await using var fixture = await StartFixtureAsync();
        _output.WriteLine("Запрашиваю отсутствующий табель, строку и день, проверяю HTTP 404.");
        var addResponse = await fixture.Client.PostAsJsonAsync(
            $"{fixture.LinesUriFor(Guid.NewGuid())}",
            new AddTimesheetLineRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                EmploymentType.Main,
                false,
                Array.Empty<SaveTimesheetDayRequest>()));
        var updateMissingLineResponse = await fixture.Client.PutAsJsonAsync(
            $"{fixture.LinesUri}/999/comment",
            new UpdateDayCommentRequest(PeriodStart, "Missing line"));
        var updateMissingDayResponse = await fixture.Client.PutAsJsonAsync(
            $"{fixture.LinesUri}/1/comment",
            new UpdateDayCommentRequest(new DateTime(2026, 1, 4), "Missing day"));

        Assert.Equal(HttpStatusCode.NotFound, addResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, updateMissingLineResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, updateMissingDayResponse.StatusCode);
        _output.WriteLine("Результат: отсутствующий табель, строка и день вернули HTTP 404.");
    }

    private static async Task AssertConflictAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("Согласованный табель нельзя изменять.", body?.Error);
    }

    /// <summary>
    /// Выводит имя сценария в лог интеграционного теста.
    /// </summary>
    private void LogScenario([System.Runtime.CompilerServices.CallerMemberName] string testName = "")
    {
        _output.WriteLine($"=== Сценарий: {testName} ===");
    }

    private Task<ApiFixture> StartFixtureAsync(
        bool seedTimesheet = true,
        TimesheetStatus status = TimesheetStatus.Draft,
        Guid? retailId = null) =>
        ApiFixture.StartAsync(_output, seedTimesheet, status, retailId);

    private sealed class ApiFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _database;
        private readonly WebApplication _application;

        private ApiFixture(
            SqliteConnection database,
            WebApplication application,
            HttpClient client,
            Guid retailId)
        {
            _database = database;
            _application = application;
            Client = client;
            RetailId = retailId;
        }

        public HttpClient Client { get; }
        public Guid RetailId { get; }
        public string LinesUri => LinesUriFor(RetailId);

        public string LinesUriFor(Guid retailId) =>
            $"api/timesheets/{retailId:D}/{PeriodStart:yyyy-MM-dd}/lines";

        public TimesheetViewModel CreateViewModel()
        {
            var apiService = new TimeSheetApiService(Client);
            return new TimesheetViewModel(new TimesheetWorkspace(apiService, RetailId));
        }

        public Task<HttpResponseMessage> GetRawTimesheetAsync() => GetRawTimesheetAsync(RetailId);

        public async Task<HttpResponseMessage> GetRawTimesheetAsync(Guid retailId) =>
            await Client.GetAsync(
                $"api/timesheets?retailId={retailId:D}&periodStart={PeriodStart:yyyy-MM-dd}");

        public Task<TimesheetResponse> GetTimesheetAsync() => GetTimesheetAsync(RetailId);

        public async Task<TimesheetResponse> GetTimesheetAsync(Guid retailId)
        {
            using var response = await GetRawTimesheetAsync(retailId);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<TimesheetResponse>())!;
        }

        public SaveTimesheetRequest CreateSaveRequest(TimesheetResponse source, TimesheetStatus status) =>
            new(
                source.RetailId,
                source.PeriodStart,
                status,
                source.Lines.Select(line => new SaveTimesheetLineRequest(
                    line.EmployeeId,
                    line.PositionId,
                    line.EmploymentType,
                    line.IsNight,
                    line.Days.Select(day => new SaveTimesheetDayRequest(
                        day.Date,
                        day.Hours,
                        day.DayType,
                        day.AbsenceCode,
                        day.Comment)).ToArray())).ToArray());

        public static async Task<ApiFixture> StartAsync(
            ITestOutputHelper output,
            bool seedTimesheet = true,
            TimesheetStatus status = TimesheetStatus.Draft,
            Guid? retailId = null)
        {
            var database = new SqliteConnection("Data Source=:memory:");
            await database.OpenAsync();

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
            builder.Services
                .AddControllers()
                .AddApplicationPart(typeof(TimesheetsController).Assembly);
            builder.Services.AddDbContext<TimesheetDbContext>(options => options.UseSqlite(database));
            builder.Services.AddScoped<TimesheetService>();

            var application = builder.Build();
            using (var scope = application.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<TimesheetDbContext>();
                await db.Database.EnsureCreatedAsync();
                if (seedTimesheet)
                {
                    TimesheetSeeder.Seed(db);
                    if (status == TimesheetStatus.Approved)
                    {
                        var timesheet = await db.Timesheets.SingleAsync();
                        timesheet.Status = TimesheetStatus.Approved;
                        await db.SaveChangesAsync();
                    }
                }
            }

            application.MapControllers();
            await application.StartAsync();
            var server = application.Services.GetRequiredService<IServer>();
            var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            output.WriteLine($"Подняты API и SQLite in-memory: {address}");
            var client = new HttpClient(new LoggingHandler(output))
            {
                BaseAddress = new Uri(address)
            };
            return new ApiFixture(
                database,
                application,
                client,
                retailId ?? TimesheetSeeder.RetailId);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _application.StopAsync();
            await _application.DisposeAsync();
            await _database.DisposeAsync();
        }
    }

    private sealed class LoggingHandler : DelegatingHandler
    {
        private readonly ITestOutputHelper _output;

        public LoggingHandler(ITestOutputHelper output) : base(new HttpClientHandler())
        {
            _output = output;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _output.WriteLine($"HTTP -> {request.Method} {request.RequestUri}");
            if (request.Content is not null)
            {
                _output.WriteLine($"HTTP request body: {await request.Content.ReadAsStringAsync()}");
            }

            var response = await base.SendAsync(request, cancellationToken);
            _output.WriteLine($"HTTP <- {(int)response.StatusCode} {response.StatusCode}");
            if (response.Content is not null)
            {
                var body = await response.Content.ReadAsStringAsync();
                _output.WriteLine($"HTTP response body: {body}");
            }

            return response;
        }
    }
}
