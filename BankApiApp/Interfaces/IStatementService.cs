using BankApiApp.DTOs.Statement;

namespace BankApiApp.Services;

public interface IStatementService
{
    Task<StatementDto> GenerateAsync(string userId, StatementRequestDto dto);
    Task<byte[]> GeneratePdfAsync(string userId, StatementRequestDto dto);
}