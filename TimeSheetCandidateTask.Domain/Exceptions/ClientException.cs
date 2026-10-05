namespace TimeSheetCandidateTask.Domain.Exceptions;

public abstract class ClientException : Exception
{
    protected ClientException(string message) : base(message)
    {
    }
}