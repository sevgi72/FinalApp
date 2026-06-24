namespace BankApiApp.DTOs;

public class ErrorResponseDto
{
    public int StatusCode { get; set; }
    public string Error { get; set; } = string.Empty;
}