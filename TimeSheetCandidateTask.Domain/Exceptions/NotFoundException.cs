namespace TimeSheetCandidateTask.Domain.Exceptions;

public class NotFoundException : ClientException
{
    public NotFoundException(string message) : base(message)
    {
    }
}
