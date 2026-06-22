namespace BankApiApp.Exceptions;

// 404 — tapılmayan resurs (hesab, kart, kredit yoxdursa)
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}