namespace BankApiApp.Exceptions;

// 404 — resurs tapılmadı (hesab, kart, kredit, əməliyyat yoxdursa)
public class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message, 404) { }
}