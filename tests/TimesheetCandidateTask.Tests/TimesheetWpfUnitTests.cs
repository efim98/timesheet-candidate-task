using System.Net.Http;
using TimeSheetCandidateTask.Domain.Exceptions;
using TimeSheetCandidateTask.Domain.Models;
using TimesheetCandidateTask.Shared;
using TimesheetCandidateTask.Wpf.Application;
using TimesheetCandidateTask.Wpf.Infrastructure;
using TimesheetCandidateTask.Wpf.ViewModels;
using Xunit;
using Xunit.Abstractions;

namespace TimesheetCandidateTask.Tests;

/// <summary>
/// Проверяет поведение ViewModel и команд WPF: загрузку, редактирование комментариев, добавление сотрудников, утверждение и обработку ошибок.
/// </summary>
public sealed class TimesheetWpfUnitTests
{
    private static readonly DateTime PeriodStart = new(2026, 1, 1);
    private readonly ITestOutputHelper _output;

    public TimesheetWpfUnitTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Проверяет, что команды редактирования выключены до загрузки табеля.
    /// </summary>
    [Fact]
    public void Commands_are_disabled_until_timesheet_is_loaded()
    {
        LogScenario();
        var service = new FakeTimeSheetApiService();
        var viewModel = CreateViewModel(service);
        _output.WriteLine("Входные данные: ViewModel создана, табель ещё не загружен.");

        Assert.False(viewModel.IsLoaded);
        Assert.True(viewModel.AreCommentsReadOnly);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
        Assert.False(viewModel.AddEmployeeCommand.CanExecute(null));
        Assert.False(viewModel.ApproveCommand.CanExecute(null));
        _output.WriteLine("Результат: команды сохранения, добавления и утверждения выключены.");
    }

    /// <summary>
    /// Проверяет загрузку черновика и доступность команд после загрузки.
    /// </summary>
    [Fact]
    public void Load_displays_draft_and_enables_commands()
    {
        LogScenario();
        var service = new FakeTimeSheetApiService();
        var viewModel = CreateViewModel(service);
        _output.WriteLine($"Входные данные API: {Describe(service.Timesheet)}.");

        viewModel.LoadCommand.Execute(null);

        Assert.True(viewModel.IsLoaded);
        Assert.Equal("Табель загружен.", viewModel.Message);
        Assert.Equal("Черновик", viewModel.StatusText);
        Assert.Single(viewModel.Lines);
        Assert.True(viewModel.SaveCommand.CanExecute(null));
        Assert.True(viewModel.AddEmployeeCommand.CanExecute(null));
        Assert.True(viewModel.ApproveCommand.CanExecute(null));
        Assert.False(viewModel.AreCommentsReadOnly);
        Assert.Equal(1, service.GetCalls);
        _output.WriteLine($"Результат: загружено строк={viewModel.Lines.Count}, status={viewModel.StatusText}.");
    }

    /// <summary>
    /// Проверяет создание локального табеля при ответе API 404.
    /// </summary>
    [Fact]
    public void Load_uses_local_new_timesheet_when_api_returns_not_found()
    {
        LogScenario();
        var service = new FakeTimeSheetApiService
        {
            GetException = new NotFoundException("Табель не найден.")
        };
        var viewModel = CreateViewModel(service);
        _output.WriteLine("Входные данные API: GET выбрасывает NotFoundException (404).");

        viewModel.LoadCommand.Execute(null);

        Assert.True(viewModel.IsLoaded);
        Assert.Equal("Табель загружен.", viewModel.Message);
        Assert.Single(viewModel.Lines);
        viewModel.SaveCommand.Execute(null);
        Assert.Equal(1, service.SaveCalls);
        Assert.Equal("Изменения сохранены.", viewModel.Message);
        _output.WriteLine("Результат: созданный локально табель отправлен через SaveAsync.");
    }

    /// <summary>
    /// Проверяет отображение ошибки загрузки без пометки табеля как загруженного.
    /// </summary>
    [Fact]
    public void Load_failure_displays_error_without_marking_timesheet_loaded()
    {
        LogScenario();
        var service = new FakeTimeSheetApiService { GetException = new HttpRequestException("offline") };
        var viewModel = CreateViewModel(service);
        _output.WriteLine("Входные данные API: GET завершается сетевой ошибкой.");

        viewModel.LoadCommand.Execute(null);

        Assert.False(viewModel.IsLoaded);
        Assert.Empty(viewModel.Lines);
        Assert.Equal("Ошибка при загрузке табеля", viewModel.Message);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
        _output.WriteLine($"Результат: '{viewModel.Message}', редактирование недоступно.");
    }

    /// <summary>
    /// Проверяет пометку строки как изменённой и сохранение двух отображаемых комментариев.
    /// </summary>
    [Fact]
    public void Editing_comment_marks_line_dirty_and_save_sends_both_displayed_comments()
    {
        LogScenario();
        var service = new FakeTimeSheetApiService();
        var viewModel = CreateViewModel(service);
        viewModel.LoadCommand.Execute(null);
        var line = Assert.Single(viewModel.Lines);
        _output.WriteLine(
            $"Входные данные: lineId={line.Line.Id}, first='{line.FirstDayComment}', " +
            $"second='{line.SecondDayComment}'. Меняю первый комментарий.");

        line.FirstDayComment = "Исправленный комментарий";
        Assert.True(line.IsDirty);
        viewModel.SaveCommand.Execute(null);

        Assert.Equal(2, service.UpdateCommentCalls);
        Assert.Contains(service.UpdatedComments, update =>
            update.Date == PeriodStart && update.Comment == "Исправленный комментарий");
        Assert.Contains(service.UpdatedComments, update =>
            update.Date == PeriodStart.AddDays(1) && update.Comment == "Второй день");
        Assert.Equal("Изменения сохранены.", viewModel.Message);
        Assert.False(Assert.Single(viewModel.Lines).IsDirty);
        _output.WriteLine(
            $"Результат: API получил {service.UpdateCommentCalls} комментария, строка отмечена чистой.");
    }

    /// <summary>
    /// Проверяет, что новая строка сохраняется через API добавления строки.
    /// </summary>
    [Fact]
    public void Save_of_new_line_uses_add_line_api_call()
    {
        LogScenario();
        var service = new FakeTimeSheetApiService();
        var viewModel = CreateViewModel(service);
        viewModel.LoadCommand.Execute(null);
        viewModel.AddEmployeeCommand.Execute(null);
        var newEmployeeId = viewModel.Lines.Last().Line.EmployeeId;
        _output.WriteLine($"Входные данные: новая строка employeeId={newEmployeeId}.");

        viewModel.SaveCommand.Execute(null);

        Assert.Equal(1, service.AddLineCalls);
        Assert.Contains(service.AddedEmployeeIds, id => id == newEmployeeId);
        Assert.All(viewModel.Lines, line => Assert.False(line.IsNew));
        _output.WriteLine($"Результат: вызван AddLineAsync; строки в ViewModel={viewModel.Lines.Count}.");
    }

    /// <summary>
    /// Проверяет отключение команды добавления после исчерпания списка сотрудников.
    /// </summary>
    [Fact]
    public void Add_employee_disables_command_after_all_seeded_employees_are_used()
    {
        LogScenario();
        var service = new FakeTimeSheetApiService();
        var viewModel = CreateViewModel(service);
        viewModel.LoadCommand.Execute(null);
        _output.WriteLine($"Входные данные: доступно сотрудников={EmployeesCollection.Employees.Count}.");

        while (viewModel.AddEmployeeCommand.CanExecute(null))
        {
            viewModel.AddEmployeeCommand.Execute(null);
        }

        Assert.Equal(EmployeesCollection.Employees.Count, viewModel.Lines.Count);
        Assert.False(viewModel.AddEmployeeCommand.CanExecute(null));
        Assert.Equal("Все доступные сотрудники уже добавлены.", viewModel.Message);
        _output.WriteLine($"Результат: добавлено строк={viewModel.Lines.Count}; команда выключена.");
    }

    /// <summary>
    /// Проверяет утверждение табеля и блокировку всех команд изменения.
    /// </summary>
    [Fact]
    public void Approve_saves_status_and_disables_all_mutation_commands()
    {
        LogScenario();
        var service = new FakeTimeSheetApiService();
        var viewModel = CreateViewModel(service);
        viewModel.LoadCommand.Execute(null);
        _output.WriteLine("Входные данные: загруженный черновик. Выполняю ApproveCommand.");

        viewModel.ApproveCommand.Execute(null);

        Assert.Equal(1, service.SaveCalls);
        Assert.Equal(TimesheetStatus.Approved, service.LastSavedTimesheet!.Status);
        Assert.Equal("Табель утверждён.", viewModel.Message);
        Assert.True(viewModel.AreCommentsReadOnly);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
        Assert.False(viewModel.AddEmployeeCommand.CanExecute(null));
        Assert.False(viewModel.ApproveCommand.CanExecute(null));
        _output.WriteLine($"Результат: SaveAsync получил status={service.LastSavedTimesheet.Status}; UI read-only.");
    }

    /// <summary>
    /// Проверяет отображение сообщения конфликта API при сохранении.
    /// </summary>
    [Fact]
    public void Save_surfaces_api_client_conflict_message()
    {
        LogScenario();
        var service = new FakeTimeSheetApiService
        {
            UpdateCommentException = new AlreadyExistsException("Согласованный табель нельзя изменять.")
        };
        var viewModel = CreateViewModel(service);
        viewModel.LoadCommand.Execute(null);
        Assert.Single(viewModel.Lines).FirstDayComment = "Изменение";
        _output.WriteLine("Входные данные: UpdateCommentAsync отвечает конфликтом 409.");

        viewModel.SaveCommand.Execute(null);

        Assert.Equal("Согласованный табель нельзя изменять.", viewModel.Message);
        _output.WriteLine($"Результат: ViewModel показала API сообщение '{viewModel.Message}'.");
    }

    /// <summary>
    /// Проверяет обработку сетевых ошибок и таймаутов.
    /// </summary>
    [Fact]
    public void Save_displays_network_and_timeout_errors()
    {
        LogScenario();
        var networkService = new FakeTimeSheetApiService
        {
            UpdateCommentException = new HttpRequestException("offline")
        };
        var networkViewModel = CreateViewModel(networkService);
        networkViewModel.LoadCommand.Execute(null);
        Assert.Single(networkViewModel.Lines).FirstDayComment = "Изменение";
        _output.WriteLine("Входные данные: UpdateCommentAsync выбрасывает HttpRequestException.");

        networkViewModel.SaveCommand.Execute(null);

        Assert.Equal("Не удалось связаться с API при сохранении.", networkViewModel.Message);
        _output.WriteLine($"Результат: '{networkViewModel.Message}'.");

        var timeoutService = new FakeTimeSheetApiService
        {
            UpdateCommentException = new TaskCanceledException("timeout")
        };
        var timeoutViewModel = CreateViewModel(timeoutService);
        timeoutViewModel.LoadCommand.Execute(null);
        Assert.Single(timeoutViewModel.Lines).FirstDayComment = "Изменение";
        _output.WriteLine("Входные данные: UpdateCommentAsync завершается таймаутом.");

        timeoutViewModel.SaveCommand.Execute(null);

        Assert.Equal("Запрос к API превысил время ожидания при сохранении.", timeoutViewModel.Message);
        _output.WriteLine($"Результат: '{timeoutViewModel.Message}'.");
    }

    /// <summary>
    /// Выводит имя сценария в лог теста для удобства диагностики.
    /// </summary>
    private void LogScenario([System.Runtime.CompilerServices.CallerMemberName] string testName = "") =>
        _output.WriteLine($"=== WPF unit: {testName} ===");

    /// <summary>
    /// Создаёт ViewModel для сценария WPF с заданным фейковым API-сервисом.
    /// </summary>
    private TimesheetViewModel CreateViewModel(FakeTimeSheetApiService service) =>
        new(new TimesheetWorkspace(service, service.Timesheet.RetailId));

    /// <summary>
    /// Формирует человекочитаемое описание табеля для логов.
    /// </summary>
    private static string Describe(Timesheet timesheet) =>
        $"retailId={timesheet.RetailId}, period={timesheet.PeriodStart:yyyy-MM-dd}, " +
        $"status={timesheet.Status}, lines={timesheet.Lines.Count}";

    private sealed class FakeTimeSheetApiService : ITimeSheetApiService
    {
        public Timesheet Timesheet { get; } = CreateTimesheet();
        public Exception? GetException { get; init; }
        public Exception? UpdateCommentException { get; init; }
        public int GetCalls { get; private set; }
        public int SaveCalls { get; private set; }
        public int AddLineCalls { get; private set; }
        public int UpdateCommentCalls { get; private set; }
        public Timesheet? LastSavedTimesheet { get; private set; }
        public List<Guid> AddedEmployeeIds { get; } = new();
        public List<(int LineId, DateTime Date, string? Comment)> UpdatedComments { get; } = new();

        public Task<Timesheet> GetAsync(
            Guid retailId,
            DateTime periodStart,
            CancellationToken cancellationToken = default)
        {
            GetCalls++;
            if (GetException is not null)
            {
                return Task.FromException<Timesheet>(GetException);
            }

            return Task.FromResult(Timesheet);
        }

        public Task<Timesheet> SaveAsync(Timesheet timesheet, CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            timesheet.Id = timesheet.Id == 0 ? 1 : timesheet.Id;
            LastSavedTimesheet = timesheet;
            return Task.FromResult(timesheet);
        }

        public Task<Timesheet> AddLineAsync(
            Guid retailId,
            DateTime periodStart,
            TimesheetLine line,
            CancellationToken cancellationToken = default)
        {
            AddLineCalls++;
            AddedEmployeeIds.Add(line.EmployeeId);
            line.Id = Timesheet.Lines.Max(item => item.Id) + 1;
            Timesheet.Lines.Add(line);
            return Task.FromResult(Timesheet);
        }

        public Task<Timesheet> UpdateCommentAsync(
            Guid retailId,
            DateTime periodStart,
            int lineId,
            DateTime date,
            string? comment,
            CancellationToken cancellationToken = default)
        {
            UpdateCommentCalls++;
            UpdatedComments.Add((lineId, date.Date, comment));
            if (UpdateCommentException is not null)
            {
                return Task.FromException<Timesheet>(UpdateCommentException);
            }

            var day = Timesheet.Lines.Single(line => line.Id == lineId)
                .Days.Single(day => day.Date.Date == date.Date);
            day.Comment = comment;
            return Task.FromResult(Timesheet);
        }

        private static Timesheet CreateTimesheet() => new()
        {
            Id = 1,
            RetailId = Guid.NewGuid(),
            PeriodStart = PeriodStart,
            Status = TimesheetStatus.Draft,
            Lines = new List<TimesheetLine>
            {
                new()
                {
                    Id = 1,
                    EmployeeId = Guid.Parse("e8abb9fa-b564-41ae-8d1f-fbbc99c0f54e"),
                    PositionId = Guid.NewGuid(),
                    EmploymentType = EmploymentType.Main,
                    Days = new List<TimesheetDay>
                    {
                        new() { Date = PeriodStart, Hours = 8, DayType = TimesheetDayType.Holiday, Comment = "Первый день" },
                        new() { Date = PeriodStart.AddDays(1), Hours = 7, DayType = TimesheetDayType.Workday, Comment = "Второй день" }
                    }
                }
            }
        };
    }
}
