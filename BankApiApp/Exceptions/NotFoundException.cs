namespace BankApiApp.Exceptions;

// 404 — resurs tapilmadi (hesab, kart, kredit, emeliyyat yoxdursa)
public class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message, 404) { }
}