using TimeSheetCandidateTask.Domain.Models;
using TimesheetCandidateTask.Shared.Utils;
using Xunit;

namespace TimesheetCandidateTask.Tests;

/// <summary>
/// Проверяет базовые вычисления табеля: статус, суммарные часы и корректную передачу данных по дням и строкам.
/// </summary>
public sealed class TimesheetPeriodTests
{
    /// <summary>
    /// Проверяет, что новый табель создаётся в черновике без строк.
    /// </summary>
    [Fact]
    public void New_timesheet_starts_as_draft_with_no_lines()
    {
        var timesheet = new Timesheet
        {
            RetailId = Guid.NewGuid(),
            PeriodStart = new DateTime(2026, 2, 1)
        };

        Assert.Equal(TimesheetStatus.Draft, timesheet.Status);
        Assert.Empty(timesheet.Lines);
    }

    /// <summary>
    /// Проверяет корректный расчёт часов для обычного рабочего дня.
    /// </summary>
    [Fact]
    public void Ordinary_workday_is_included_in_line_totals()
    {
        var line = new TimesheetLine
        {
            Days = new List<TimesheetDay>
            {
                new() { Date = new DateTime(2026, 2, 2), Hours = 8, DayType = TimesheetDayType.Workday }
            }
        };

        var totals = TimesheetCalculator.Calculate(line);

        Assert.Equal(8, totals.WorkedHours);
        Assert.Equal(0, totals.HolidayHours);
        Assert.Equal(8, totals.TotalHours);
    }

    /// <summary>
    /// Проверяет сохранение данных об отсутствии и комментарии для дня.
    /// </summary>
    [Fact]
    public void Day_preserves_its_absence_details()
    {
        var day = new TimesheetDay
        {
            Date = new DateTime(2026, 2, 3),
            Hours = 0,
            DayType = TimesheetDayType.Absence,
            AbsenceCode = "Б",
            Comment = "Больничный"
        };

        Assert.Equal("Б", day.AbsenceCode);
        Assert.Equal("Больничный", day.Comment);
    }
}
