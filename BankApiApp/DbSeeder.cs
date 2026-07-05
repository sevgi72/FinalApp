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
        var context = services.GetRequiredService<AppDbContext>();   // ← elave olundu

        // rollari yarat
        // rollari yarat
        foreach (var role in new[] { "SuperAdmin", "Admin", "Customer" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // admin istifadecisi yarat
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
        // superadmin istifadecisi yarat
        var superAdminEmail = "superadmin@bank.az";
        if (await userManager.FindByEmailAsync(superAdminEmail) == null)
        {
            var superAdmin = new ApplicationUser
            {
                FullName = "Super Admin",
                Email = superAdminEmail,
                UserName = superAdminEmail,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(superAdmin, "Super123!");
            await userManager.AddToRoleAsync(superAdmin, "SuperAdmin");
        }
        // Kommunal provayderleri seed et (yoxdursa)
        if (!await context.UtilityProviders.AnyAsync())
        {
            var providers = new List<UtilityProvider>
            {
                new() { Name = "Azerisiq", Category = UtilityCategory.Electricity },
                new() { Name = "Azersu", Category = UtilityCategory.Water },
                new() { Name = "Azeriqaz", Category = UtilityCategory.Gas },
                new() { Name = "Azercell", Category = UtilityCategory.Mobile },
                new() { Name = "Bakcell", Category = UtilityCategory.Mobile },
                new() { Name = "Nar", Category = UtilityCategory.Mobile },
                new() { Name = "Aztelecom", Category = UtilityCategory.Internet },
                new() { Name = "BIRLink", Category = UtilityCategory.Internet },
                new() { Name = "AileTV", Category = UtilityCategory.TV },
                new() { Name = "KATV", Category = UtilityCategory.TV }
            };
            await context.UtilityProviders.AddRangeAsync(providers);
            await context.SaveChangesAsync();
        }
    }
}