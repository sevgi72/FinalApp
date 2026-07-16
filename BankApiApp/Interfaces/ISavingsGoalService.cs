using BankApiApp.DTOs.SavingsGoal;

namespace BankApiApp.Services;

public interface ISavingsGoalService
{
    Task<SavingsGoalDto> CreateAsync(string userId, CreateSavingsGoalDto dto);
    Task<List<SavingsGoalDto>> GetMyAsync(string userId);
    Task<SavingsGoalDto> ContributeAsync(string userId, Guid goalId, ContributeGoalDto dto);
    Task<string> WithdrawAsync(string userId, Guid goalId, ContributeGoalDto dto);
    Task<string> DeleteAsync(string userId, Guid goalId);
    Task<SavingsGoalDto> UpdateAsync(string userId, Guid goalId, UpdateSavingsGoalDto dto);
}