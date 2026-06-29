using BankApiApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BankApiApp.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var context = services.GetRequiredService<AppDbContext>();   // ← əlavə olundu

        // rolları yarat
        foreach (var role in new[] { "Admin", "Customer" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // admin istifadəçisi yarat
        var adminEmail = "admin@bank.az";
        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var admin = new ApplicationUser
            {
                FullName = "Admin",
                Email = adminEmail,
                UserName = adminEmail,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(admin, "Admin123!");
            await userManager.AddToRoleAsync(admin, "Admin");
        }

        // Kommunal provayderləri seed et (yoxdursa)
        if (!await context.UtilityProviders.AnyAsync())
        {
            var providers = new List<UtilityProvider>
            {
                new() { Name = "Azərişıq", Category = UtilityCategory.Electricity },
                new() { Name = "Azərsu", Category = UtilityCategory.Water },
                new() { Name = "Azəriqaz", Category = UtilityCategory.Gas },
                new() { Name = "Azercell", Category = UtilityCategory.Mobile },
                new() { Name = "Bakcell", Category = UtilityCategory.Mobile },
                new() { Name = "Nar", Category = UtilityCategory.Mobile },
                new() { Name = "Aztelecom", Category = UtilityCategory.Internet },
                new() { Name = "BIRLink", Category = UtilityCategory.Internet },
                new() { Name = "AiləTV", Category = UtilityCategory.TV },
                new() { Name = "KATV", Category = UtilityCategory.TV }
            };
            await context.UtilityProviders.AddRangeAsync(providers);
            await context.SaveChangesAsync();
        }
    }
}