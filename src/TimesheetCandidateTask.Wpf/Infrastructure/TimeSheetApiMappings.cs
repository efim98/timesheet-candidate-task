using TimeSheetCandidateTask.Domain.Models;
using TimesheetCandidateTask.Shared.Contracts;

namespace TimesheetCandidateTask.Wpf.Infrastructure;

internal static class TimeSheetApiMappings
{
    public static Timesheet ToDomain(this TimesheetResponse source) => new()
    {
        Id = source.Id,
        RetailId = source.RetailId,
        PeriodStart = source.PeriodStart,
        Status = source.Status,
        Lines = source.Lines.Select(line => new TimesheetLine
        {
            Id = line.Id,
            TimesheetId = source.Id,
            EmployeeId = line.EmployeeId,
            PositionId = line.PositionId,
            EmploymentType = line.EmploymentType,
            IsNight = line.IsNight,
            Days = line.Days.Select(day => new TimesheetDay
            {
                TimesheetLineId = line.Id,
                Date = day.Date,
                Hours = day.Hours,
                DayType = day.DayType,
                AbsenceCode = day.AbsenceCode,
                Comment = day.Comment
            }).ToList()
        }).ToList()
    };

    public static SaveTimesheetRequest ToSaveRequest(this Timesheet source) => new(
        source.RetailId,
        source.PeriodStart,
        source.Status,
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
                day.Comment)).ToList())).ToList());

    public static AddTimesheetLineRequest ToAddRequest(this TimesheetLine source) => new(
        source.EmployeeId,
        source.PositionId,
        source.EmploymentType,
        source.IsNight,
        source.Days.Select(day => new SaveTimesheetDayRequest(
            day.Date,
            day.Hours,
            day.DayType,
            day.AbsenceCode,
            day.Comment)).ToList());
}
