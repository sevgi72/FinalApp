namespace BankApiApp.Exceptions;

// bütün öz xətalarımızın bazası — status kodunu özü daşıyır
public abstract class AppException : Exception
{
    public int StatusCode { get; }

    protected AppException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }
}