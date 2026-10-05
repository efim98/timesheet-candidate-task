namespace TimeSheetCandidateTask.Domain.Exceptions;

public class AlreadyExistsException : ClientException
{
    public AlreadyExistsException(string message) : base(message)
    {
    }
}