using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using TimeSheetCandidateTask.Domain.Exceptions;
using TimeSheetCandidateTask.Domain.Models;
using TimesheetCandidateTask.Shared.Contracts;

namespace TimesheetCandidateTask.Wpf.Infrastructure;

public sealed class TimeSheetApiService : ITimeSheetApiService
{
    private readonly HttpClient _httpClient;

    public TimeSheetApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Timesheet> GetAsync(
        Guid retailId,
        DateTime periodStart,
        CancellationToken cancellationToken = default)
    {
        var uri = $"api/timesheets?retailId={retailId:D}&periodStart={FormatDate(periodStart)}";
        using var response = await _httpClient.GetAsync(uri, cancellationToken);
        await EnsureSuccessStatusCodeAsync(response, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<TimesheetResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("The timesheets API returned an empty response.");
        return result.ToDomain();
    }

    public async Task<Timesheet> SaveAsync(Timesheet timesheet, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PutAsJsonAsync(
            "api/timesheets",
            timesheet.ToSaveRequest(),
            cancellationToken);
        await EnsureSuccessStatusCodeAsync(response, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<TimesheetResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("The timesheets API returned an empty response.");
        return result.ToDomain();
    }

    public async Task<Timesheet> AddLineAsync(
        Guid retailId,
        DateTime periodStart,
        TimesheetLine line,
        CancellationToken cancellationToken = default)
    {
        var uri = $"api/timesheets/{retailId:D}/{FormatDate(periodStart)}/lines";
        using var response = await _httpClient.PostAsJsonAsync(uri, line.ToAddRequest(), cancellationToken);
        await EnsureSuccessStatusCodeAsync(response, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<TimesheetResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("The timesheets API returned an empty response.");
        return result.ToDomain();
    }

    public async Task<Timesheet> UpdateCommentAsync(
        Guid retailId,
        DateTime periodStart,
        int lineId,
        DateTime date,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        var uri = $"api/timesheets/{retailId:D}/{FormatDate(periodStart)}/lines/{lineId}/comment";
        var request = new UpdateDayCommentRequest(date, comment);
        using var response = await _httpClient.PutAsJsonAsync(uri, request, cancellationToken);
        await EnsureSuccessStatusCodeAsync(response, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<TimesheetResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("The timesheets API returned an empty response.");
        return result.ToDomain();
    }

    private static string FormatDate(DateTime value) =>
        value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static async Task EnsureSuccessStatusCodeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken: cancellationToken);
        var message = error?.Error
            ?? response.ReasonPhrase
            ?? $"API request failed with status code {(int)response.StatusCode}.";

        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                throw new BusinessRuleException(message);
            case HttpStatusCode.NotFound:
                throw new NotFoundException(message);
            case HttpStatusCode.Conflict:
                throw new AlreadyExistsException(message);
            default:
                response.EnsureSuccessStatusCode();
                break;
        }
    }
}
