using TimesheetCandidateTask.Api.Infrastructure;
using TimeSheetCandidateTask.Domain.Exceptions;
using TimeSheetCandidateTask.Domain.Models;
using TimesheetCandidateTask.Shared;
using TimesheetCandidateTask.Wpf.Infrastructure;

namespace TimesheetCandidateTask.Wpf.Application;


public sealed class TimesheetWorkspace
{
    private static readonly DateTime FirstDayOfYear = new(2026, 1, 1);
    private static readonly DateTime SecondDayOfYear = new(2026, 1, 2);
    private static readonly DateTime ThirdDayOfYear = new(2026, 1, 3);

    private readonly ITimeSheetApiService _apiService;
    private readonly Guid _retailId;
    private int _nextTemporaryLineId = -1;
    private Timesheet? _timesheet;
    private bool _isNew;
    private bool _approvalPending;
    private readonly List<TimesheetLine> _linesToCreate = new();
    private readonly Dictionary<(int LineId, DateTime Date), string?> _daysToUpdate = new();

    public TimesheetWorkspace(ITimeSheetApiService apiService, Guid retailId)
    {
        _apiService = apiService;
        _retailId = retailId;
    }

    public Timesheet Load()
    {
        try
        {
            var timesheet = Task.Run(() => _apiService.GetAsync(_retailId, FirstDayOfYear)).GetAwaiter().GetResult();
            _timesheet = timesheet;
            return timesheet;
        }
        catch (NotFoundException)
        {
            var timesheet = CreateSeededTimesheet();
            _isNew = true;
            _timesheet = timesheet;
            return timesheet;
        }
        catch (Exception exception)
        {
            throw new Exception("Ошибка при загрузке табеля", exception);
        }
    }

    public TimesheetLine AddEmployee()
    {
        var timesheet = RequireTimesheet();
        var availableEmployee = EmployeesCollection.Employees
            .FirstOrDefault(employee => timesheet.Lines.All(line => line.EmployeeId != employee.Key));
        if (availableEmployee.Value is null)
        {
            throw new InvalidOperationException("Нет доступных сотрудников для добавления.");
        }

        var line = new TimesheetLine
        {
            Id = _nextTemporaryLineId--,
            EmployeeId = availableEmployee.Key,
            PositionId = availableEmployee.Value.PositionId,
            EmploymentType = availableEmployee.Value.EmploymentType,
            Days = new List<TimesheetDay>
            {
                new() { Date = FirstDayOfYear, Hours = 8, DayType = TimesheetDayType.Holiday },
                new() { Date = ThirdDayOfYear, Hours = 8, DayType = TimesheetDayType.Workday },
                new() { Date = SecondDayOfYear, Hours = 0, DayType = TimesheetDayType.Weekend }
            }
        };

        timesheet.Lines.Add(line);
        _linesToCreate.Add(line);
        return line;
    }

    public void UpdateComment(int lineId, DateTime date, string? comment)
    {
        var line = RequireTimesheet().Lines.Single(item => item.Id == lineId);
        var day = line.Days.SingleOrDefault(item => item.Date.Date == date.Date)
            ?? throw new KeyNotFoundException();
        day.Comment = comment;
        if (!_linesToCreate.Contains(line))
        {
            _daysToUpdate[(lineId, date.Date)] = comment;
        }
    }

    public Timesheet Approve()
    {
        var timesheet = RequireTimesheet();
        timesheet.Status = TimesheetStatus.Approved;
        _approvalPending = true;
        return timesheet;
    }

    public Timesheet SaveAndGetResult()
    {
        return Task.Run(SaveAndGetResultAsync).GetAwaiter().GetResult();
    }

    private async Task<Timesheet> SaveAndGetResultAsync()
    {
        var timesheet = RequireTimesheet();
        if (_isNew)
        {
            _timesheet = await _apiService.SaveAsync(timesheet);
            _isNew = false;
            _approvalPending = false;
            _linesToCreate.Clear();
            _daysToUpdate.Clear();
            return _timesheet;
        }

        foreach (var line in _linesToCreate.ToList())
        {
            timesheet = await _apiService.AddLineAsync(
                _retailId,
                timesheet.PeriodStart,
                line);
            _timesheet = timesheet;
            _linesToCreate.Remove(line);
        }

        foreach (var dayUpdate in _daysToUpdate.ToList())
        {
            timesheet = await _apiService.UpdateCommentAsync(
                _retailId,
                timesheet.PeriodStart,
                dayUpdate.Key.LineId,
                dayUpdate.Key.Date,
                dayUpdate.Value);
            _timesheet = timesheet;
            _daysToUpdate.Remove(dayUpdate.Key);
        }

        if (_approvalPending)
        {
            timesheet = await _apiService.SaveAsync(timesheet);
            _timesheet = timesheet;
            _approvalPending = false;
        }

        return timesheet;
    }

    private Timesheet CreateSeededTimesheet() => new()
    {
        Id = 1,
        RetailId = _retailId,
        PeriodStart = new DateTime(2026, 1, 1),
        Status = TimesheetStatus.Draft,
        Lines = new List<TimesheetLine>
        {
            new()
            {
                Id = 1,
                EmployeeId = TimesheetSeeder.EmployeeId,
                PositionId = Guid.Parse("7b442775-f9ee-4cf5-b3c3-75687650a81f"),
                EmploymentType = EmploymentType.Main,
                Days = new List<TimesheetDay>
                {
                    new() { Date = new DateTime(2026, 1, 1), Hours = 8, DayType = TimesheetDayType.Holiday, Comment = "Инвентаризация" },
                    new() { Date = new DateTime(2026, 1, 2), Hours = 8, DayType = TimesheetDayType.Workday }
                }
            }
        }
    };
    private Timesheet RequireTimesheet() =>
        _timesheet ?? throw new InvalidOperationException("Сначала загрузите табель из API.");
}
