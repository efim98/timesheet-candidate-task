using Microsoft.EntityFrameworkCore;
using TimesheetCandidateTask.Api.Contracts;
using TimesheetCandidateTask.Api.Domain;
using TimesheetCandidateTask.Api.Infrastructure;

namespace TimesheetCandidateTask.Api.Application;

public sealed class TimesheetService
{
    private readonly TimesheetDbContext _db;

    public TimesheetService(TimesheetDbContext db)
    {
        _db = db;
    }

    public async Task<TimesheetResponse?> GetAsync(Guid retailId, DateTime periodStart, CancellationToken cancellationToken)
    {
        var timesheet = await FindAsync(retailId, periodStart, cancellationToken);
        return timesheet is null ? null : ToResponse(timesheet);
    }

    public async Task<TimesheetResponse> SaveAsync(SaveTimesheetRequest request, CancellationToken cancellationToken)
    {
        ValidatePeriod(request.PeriodStart);
        var timesheet = await FindAsync(request.RetailId, request.PeriodStart, cancellationToken);

        if (timesheet is null)
        {
            timesheet = new Timesheet
            {
                RetailId = request.RetailId,
                PeriodStart = request.PeriodStart.Date
            };
            _db.Timesheets.Add(timesheet);
        }
        else
        {
            EnsureEditable(timesheet);
            _db.TimesheetDays.RemoveRange(timesheet.Lines.SelectMany(line => line.Days));
            _db.TimesheetLines.RemoveRange(timesheet.Lines);
        }

        timesheet.Status = request.Status;
        timesheet.Lines = request.Lines.Select(ToEntity).ToList();
        await _db.SaveChangesAsync(cancellationToken);
        return ToResponse(timesheet);
    }

    public async Task<TimesheetResponse> AddLineAsync(Guid retailId, DateTime periodStart, AddTimesheetLineRequest request, CancellationToken cancellationToken)
    {
        var timesheet = await RequireAsync(retailId, periodStart, cancellationToken);
        EnsureEditable(timesheet);
        timesheet.Lines.Add(ToEntity(request));
        await _db.SaveChangesAsync(cancellationToken);
        return ToResponse(timesheet);
    }

    public async Task<TimesheetResponse> UpdateDayCommentAsync(Guid retailId, DateTime periodStart, int lineId, UpdateDayCommentRequest request, CancellationToken cancellationToken)
    {
        var timesheet = await RequireAsync(retailId, periodStart, cancellationToken);
        EnsureEditable(timesheet);

        var line = timesheet.Lines.SingleOrDefault(item => item.Id == lineId)
            ?? throw new KeyNotFoundException("Строка сотрудника не найдена.");
        var day = line.Days.SingleOrDefault(item => item.Date.Date == request.Date.Date.AddDays(1))
            ?? throw new KeyNotFoundException("День табеля не найден.");

        day.Comment = request.Comment;
        await _db.SaveChangesAsync(cancellationToken);
        return ToResponse(timesheet);
    }

    private async Task<Timesheet> RequireAsync(Guid retailId, DateTime periodStart, CancellationToken cancellationToken) =>
        await FindAsync(retailId, periodStart, cancellationToken)
        ?? throw new KeyNotFoundException("Табель за указанный месяц не найден.");

    private Task<Timesheet?> FindAsync(Guid retailId, DateTime periodStart, CancellationToken cancellationToken) =>
        _db.Timesheets
            .Include(item => item.Lines)
            .ThenInclude(line => line.Days)
            .SingleOrDefaultAsync(item => item.RetailId == retailId && item.PeriodStart == periodStart.Date, cancellationToken);

    private static TimesheetLine ToEntity(AddTimesheetLineRequest source) => new()
    {
        EmployeeId = source.EmployeeId,
        PositionId = source.PositionId,
        EmploymentType = source.EmploymentType,
        IsNight = source.IsNight,
        Days = source.Days.Select(ToEntity).ToList()
    };

    private static TimesheetLine ToEntity(SaveTimesheetLineRequest source) => new()
    {
        EmployeeId = source.EmployeeId,
        PositionId = source.PositionId,
        EmploymentType = source.EmploymentType,
        IsNight = source.IsNight,
        Days = source.Days.Select(ToEntity).ToList()
    };

    private static TimesheetDay ToEntity(SaveTimesheetDayRequest source) => new()
    {
        Date = source.Date.Date,
        Hours = source.Hours,
        DayType = source.DayType,
        AbsenceCode = source.AbsenceCode,
        Comment = source.Comment
    };

    private static TimesheetResponse ToResponse(Timesheet source) => new(
        source.Id,
        source.RetailId,
        source.PeriodStart,
        source.Status,
        source.Lines.Select(line => new TimesheetLineResponse(
            line.Id,
            line.EmployeeId,
            line.PositionId,
            line.EmploymentType,
            line.IsNight,
            TimesheetCalculator.Calculate(line),
            line.Days.OrderBy(day => day.Date).Select(day => new TimesheetDayResponse(day.Date, day.Hours, day.DayType, day.AbsenceCode, day.Comment)).ToList())).ToList());

    private static void ValidatePeriod(DateTime periodStart)
    {
        if (periodStart.Date.Day != 1)
        {
            throw new ArgumentException("Начало периода должно приходиться на первый день месяца.");
        }
    }

    private static void EnsureEditable(Timesheet timesheet)
    {
        if (timesheet.Status == TimesheetStatus.Approved)
        {
            throw new InvalidOperationException("Согласованный табель нельзя изменять.");
        }
    }
}
