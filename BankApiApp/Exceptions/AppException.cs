namespace BankApiApp.Exceptions;

// butun oz xetalarimizin bazasi — status kodunu ozu dasiyir
public abstract class AppException : Exception
{
    public int StatusCode { get; }

    protected AppException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }
}