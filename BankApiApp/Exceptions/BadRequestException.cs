namespace BankApiApp.Exceptions;

// 400 — biznes qaydasi pozulub (balans catmir, sahib deyil, yanlis parol ve s.)
public class BadRequestException : AppException
{
    public BadRequestException(string message) : base(message, 400) { }
}