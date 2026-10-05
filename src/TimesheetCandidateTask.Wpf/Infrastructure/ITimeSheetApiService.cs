

using TimeSheetCandidateTask.Domain.Models;

namespace TimesheetCandidateTask.Wpf.Infrastructure;

public interface ITimeSheetApiService
{
    Task<Timesheet> GetAsync(Guid retailId, DateTime periodStart, CancellationToken cancellationToken = default);

    Task<Timesheet> SaveAsync(Timesheet timesheet, CancellationToken cancellationToken = default);

    Task<Timesheet> AddLineAsync(
        Guid retailId,
        DateTime periodStart,
        TimesheetLine line,
        CancellationToken cancellationToken = default);

    Task<Timesheet> UpdateCommentAsync(
        Guid retailId,
        DateTime periodStart,
        int lineId,
        DateTime date,
        string? comment,
        CancellationToken cancellationToken = default);
}
