using TimesheetCandidateTask.Api.Domain;
using TimesheetCandidateTask.Api.Infrastructure;

namespace TimesheetCandidateTask.Wpf.Application;

public sealed class TimesheetWorkspace
{
    private Timesheet? _timesheet;

    public Timesheet Load()
    {
        _timesheet = CreateSeededTimesheet();
        return _timesheet;
    }

    public Timesheet AddEmployee()
    {
        var timesheet = RequireTimesheet();
        var source = timesheet.Lines.First();
        timesheet.Lines.Add(new TimesheetLine
        {
            Id = timesheet.Lines.Count + 1,
            EmployeeId = source.EmployeeId,
            PositionId = source.PositionId,
            EmploymentType = source.EmploymentType,
            IsNight = source.IsNight,
            Days = source.Days.Select(day => new TimesheetDay
            {
                Date = day.Date,
                Hours = day.Hours,
                DayType = day.DayType,
                AbsenceCode = day.AbsenceCode,
                Comment = day.Comment
            }).ToList()
        });
        return timesheet;
    }

    public void UpdateComment(int lineId, DateTime date, string? comment)
    {
        var line = RequireTimesheet().Lines.Single(item => item.Id == lineId);
        var day = line.Days.SingleOrDefault(item => item.Date.Date == date.Date.AddDays(1))
            ?? throw new KeyNotFoundException();
        day.Comment = comment;
    }

    public Timesheet Approve()
    {
        var timesheet = RequireTimesheet();
        timesheet.Status = TimesheetStatus.Approved;
        return timesheet;
    }

    private Timesheet RequireTimesheet() => _timesheet ?? Load();

    private static Timesheet CreateSeededTimesheet() => new()
    {
        Id = 1,
        RetailId = TimesheetSeeder.RetailId,
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
}
