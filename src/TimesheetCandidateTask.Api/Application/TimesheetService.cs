using Microsoft.EntityFrameworkCore;
using TimesheetCandidateTask.Api.Infrastructure;
using TimeSheetCandidateTask.Domain.Models;
using TimesheetCandidateTask.Shared.Contracts;
using TimesheetCandidateTask.Shared.Utils;

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
        ValidatePeriod(periodStart);
        var timesheet = await FindAsync(retailId, periodStart, cancellationToken);
        return timesheet is null ? null : ToResponse(timesheet);
    }

    public async Task<TimesheetResponse> SaveAsync(SaveTimesheetRequest request, CancellationToken cancellationToken)
    {
        ValidatePeriod(request.PeriodStart);
        EnsureUniqueEmployees(request.Lines.Select(line => line.EmployeeId));
        foreach (var line in request.Lines)
        {
            ValidateDaysInPeriod(request.PeriodStart, line.Days);
        }

        var timesheet = await FindAsync(request.RetailId, request.PeriodStart, cancellationToken);

        if (timesheet is null)
        {
            timesheet = new Timesheet
            {
                RetailId = request.RetailId,
                PeriodStart = request.PeriodStart.Date,
                Lines = request.Lines.Select(ToEntity).ToList()
            };
            _db.Timesheets.Add(timesheet);
        }
        else
        {
            EnsureEditable(timesheet);
            UpdateLines(timesheet, request.Lines);
        }

        timesheet.Status = request.Status;
        await _db.SaveChangesAsync(cancellationToken);
        return ToResponse(timesheet);
    }

    public async Task<TimesheetResponse> AddLineAsync(Guid retailId, DateTime periodStart, AddTimesheetLineRequest request, CancellationToken cancellationToken)
    {
        ValidatePeriod(periodStart);
        ValidateDaysInPeriod(periodStart, request.Days);
        var timesheet = await RequireAsync(retailId, periodStart, cancellationToken);
        EnsureEditable(timesheet);
        if (timesheet.Lines.Any(line => line.EmployeeId == request.EmployeeId))
        {
            throw new InvalidOperationException("Для этого сотрудника строка в табеле уже существует.");
        }

        timesheet.Lines.Add(ToEntity(request));
        await _db.SaveChangesAsync(cancellationToken);
        return ToResponse(timesheet);
    }

    public async Task<TimesheetResponse> UpdateDayCommentAsync(Guid retailId, DateTime periodStart, int lineId, UpdateDayCommentRequest request, CancellationToken cancellationToken)
    {
        ValidatePeriod(periodStart);
        ValidateDateInPeriod(periodStart, request.Date);
        var timesheet = await RequireAsync(retailId, periodStart, cancellationToken);
        EnsureEditable(timesheet);

        var line = timesheet.Lines.SingleOrDefault(item => item.Id == lineId)
            ?? throw new KeyNotFoundException("Строка сотрудника не найдена.");
        var day = line.Days.SingleOrDefault(item => item.Date.Date == request.Date.Date)
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

    private void UpdateLines(Timesheet timesheet, IReadOnlyCollection<SaveTimesheetLineRequest> requestedLines)
    {
        var requestedByEmployee = requestedLines.ToDictionary(line => line.EmployeeId);
        foreach (var existingLine in timesheet.Lines.ToList())
        {
            if (!requestedByEmployee.TryGetValue(existingLine.EmployeeId, out var requestedLine))
            {
                _db.TimesheetLines.Remove(existingLine);
                timesheet.Lines.Remove(existingLine);
                continue;
            }

            existingLine.PositionId = requestedLine.PositionId;
            existingLine.EmploymentType = requestedLine.EmploymentType;
            existingLine.IsNight = requestedLine.IsNight;
            UpdateDays(existingLine, requestedLine.Days);
            requestedByEmployee.Remove(existingLine.EmployeeId);
        }

        foreach (var requestedLine in requestedByEmployee.Values)
        {
            timesheet.Lines.Add(ToEntity(requestedLine));
        }
    }

    private void UpdateDays(TimesheetLine line, IReadOnlyCollection<SaveTimesheetDayRequest> requestedDays)
    {
        var requestedByDate = requestedDays.ToDictionary(day => day.Date.Date);
        foreach (var existingDay in line.Days.ToList())
        {
            if (!requestedByDate.TryGetValue(existingDay.Date.Date, out var requestedDay))
            {
                _db.TimesheetDays.Remove(existingDay);
                line.Days.Remove(existingDay);
                continue;
            }

            CopyDayValues(existingDay, requestedDay);
            requestedByDate.Remove(existingDay.Date.Date);
        }

        foreach (var requestedDay in requestedByDate.Values)
        {
            line.Days.Add(ToEntity(requestedDay));
        }
    }

    private static void CopyDayValues(TimesheetDay target, SaveTimesheetDayRequest source)
    {
        target.Hours = source.Hours;
        target.DayType = source.DayType;
        target.AbsenceCode = source.AbsenceCode;
        target.Comment = source.Comment;
    }

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

    private static void EnsureUniqueEmployees(IEnumerable<Guid> employeeIds)
    {
        if (employeeIds.Distinct().Count() != employeeIds.Count())
        {
            throw new InvalidOperationException("В табеле не может быть несколько строк одного сотрудника.");
        }
    }

    private static void ValidateDaysInPeriod(DateTime periodStart, IEnumerable<SaveTimesheetDayRequest> days)
    {
        var dates = new HashSet<DateTime>();
        foreach (var day in days)
        {
            ValidateDateInPeriod(periodStart, day.Date);
            if (!dates.Add(day.Date.Date))
            {
                throw new ArgumentException("В строке не может быть более одной записи за день.");
            }
        }
    }

    private static void ValidateDateInPeriod(DateTime periodStart, DateTime date)
    {
        if (date.Year != periodStart.Year || date.Month != periodStart.Month)
        {
            throw new ArgumentException("Дата дня должна относиться к месяцу табеля.");
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
