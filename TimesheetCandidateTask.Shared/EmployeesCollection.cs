using TimeSheetCandidateTask.Domain.Models;

namespace TimesheetCandidateTask.Shared;

public static class EmployeesCollection
{
    public static readonly IReadOnlyDictionary<Guid, EmployeeSeed> Employees =
        new Dictionary<Guid, EmployeeSeed>
        {
            [Guid.Parse("e8abb9fa-b564-41ae-8d1f-fbbc99c0f54e")] = new("Иванов И.И.", Guid.Parse("7b442775-f9ee-4cf5-b3c3-75687650a81f"), EmploymentType.Main),
            [Guid.Parse("1a000001-0000-4000-8000-000000000001")] = new("Петров П.П.", Guid.Parse("7b442775-f9ee-4cf5-b3c3-75687650a82f"), EmploymentType.Main),
            [Guid.Parse("1a000002-0000-4000-8000-000000000002")] = new("Сидорова А.А.", Guid.Parse("7b442775-f9ee-4cf5-b3c3-75687650a83f"), EmploymentType.PartTime),
            [Guid.Parse("1a000003-0000-4000-8000-000000000003")] = new("Кузнецов Д.Д.", Guid.Parse("7b442775-f9ee-4cf5-b3c3-75687650a84f"), EmploymentType.Contractor),
            [Guid.Parse("1a000004-0000-4000-8000-000000000004")] = new("Петров П.П.", Guid.Parse("7b442775-f9ee-4cf5-b3c3-75687650a85f"), EmploymentType.Contractor) // Сотрудник с одинаковым именем, но другим ID и EmploymentType
        };
    public sealed record EmployeeSeed(string EmployeeName, Guid PositionId, EmploymentType EmploymentType);
}