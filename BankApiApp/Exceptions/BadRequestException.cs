namespace BankApiApp.Exceptions;

// 400 — biznes qaydası pozulub (balans çatmır, sahib deyil, yanlış parol və s.)
public class BadRequestException : AppException
{
    public BadRequestException(string message) : base(message, 400) { }
}