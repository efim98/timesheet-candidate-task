using TimeSheetCandidateTask.Domain.Models;
using TimesheetCandidateTask.Shared.Contracts;

namespace TimesheetCandidateTask.Shared.Utils;

public static class TimesheetCalculator
{
    public static TimesheetTotalsResponse Calculate(TimesheetLine line)
    {
        var workedHours = line.Days
            .Where(day => day.DayType is TimesheetDayType.Workday)
            .Sum(day => day.Hours);
        var holidayHours = line.Days
            .Where(day => day.DayType == TimesheetDayType.Holiday)
            .Sum(day => day.Hours);

        return new TimesheetTotalsResponse(workedHours, holidayHours, workedHours + holidayHours);
    }
}
