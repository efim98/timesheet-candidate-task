namespace TimeSheetCandidateTask.Domain.Exceptions;

// 400 Bad Request (нарушение бизнес-правил)
public class BusinessRuleException : ClientException
{
    public BusinessRuleException(string message) : base(message)
    {
    }
}