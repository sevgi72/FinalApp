using BankApiApp.Models.Common;

namespace BankApiApp.Models;

public class UtilityProvider : BaseEntity   // Id buradan
{
    public string Name { get; set; } = string.Empty;        // "Azərişıq"
    public UtilityCategory Category { get; set; }           // Electricity
    public bool IsActive { get; set; } = true;              // admin söndürə bilər
}