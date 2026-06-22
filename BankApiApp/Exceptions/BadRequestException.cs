namespace BankApiApp.Exceptions;

// 400 — səhv əməliyyat (balans çatmır, sahib deyil, yanlış parol və s.)
public class BadRequestException : Exception
{
    public BadRequestException(string message) : base(message) { }
}