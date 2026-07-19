using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BankApiApp.Data;
using BankApiApp.Exceptions;
using BankApiApp.Models;
using BankApiApp.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace BankApiApp.Tests;

public class AdminServiceTests
{
    private readonly Mock<ILocalizationService> _mockLocalizer;
    private readonly Mock<INotificationService> _mockNotification;

    public AdminServiceTests()
    {
        _mockLocalizer = new Mock<ILocalizationService>();
        _mockNotification = new Mock<INotificationService>();

        // Localizer hər hansı bir açar (Key) istəyəndə elə açarın özünü mətn olaraq qaytarsın
        _mockLocalizer.Setup(l => l.Get(It.IsAny<string>())).Returns((string key) => key);
    }

    // Identity UserManager-i Mock etmək üçün köməkçi metod
    private Mock<UserManager<ApplicationUser>> MockUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    // InMemory AppDbContext yaratmaq üçün köməkçi metod
    private AppDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Hər test işləyəndə təmiz baza yaradır
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task BlockUserAsync_CallerAndTargetAreSame_ThrowsBadRequestException()
    {
        // Arrange (Hazırlıq)
        var dbContext = GetInMemoryDbContext();
        var mockUserManager = MockUserManager();

        var callerId = "user-123";
        var targetId = "user-123"; // Admin öz ID-sini hədəf seçir

        var targetUser = new ApplicationUser { Id = targetId, Email = "test@amorabank.az" };
        mockUserManager.Setup(m => m.FindByIdAsync(targetId)).ReturnsAsync(targetUser);

        var service = new AdminService(
            mockUserManager.Object, null!, dbContext, _mockNotification.Object, _mockLocalizer.Object);

        // Act & Assert (İcra və Yoxlama)
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            service.BlockUserAsync(callerId, targetId));

        Assert.Equal("CannotBlockOwnAccount", exception.Message);
    }

    [Fact]
    public async Task BlockUserAsync_NormalAdminTriesToBlockPrivilegedUser_ThrowsBadRequestException()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext();
        var mockUserManager = MockUserManager();

        var callerId = "normal-admin-id";
        var targetId = "target-admin-id";

        var callerUser = new ApplicationUser { Id = callerId };
        var targetUser = new ApplicationUser { Id = targetId, Email = "otheradmin@amorabank.az" };

        mockUserManager.Setup(m => m.FindByIdAsync(targetId)).ReturnsAsync(targetUser);
        mockUserManager.Setup(m => m.FindByIdAsync(callerId)).ReturnsAsync(callerUser);

        // Hər iki istifadəçinin rollarını simulyasiya edirik (İki adi Admin)
        mockUserManager.Setup(m => m.GetRolesAsync(targetUser)).ReturnsAsync(new List<string> { "Admin" });
        mockUserManager.Setup(m => m.GetRolesAsync(callerUser)).ReturnsAsync(new List<string> { "Admin" }); // SuperAdmin deyil!

        var service = new AdminService(
            mockUserManager.Object, null!, dbContext, _mockNotification.Object, _mockLocalizer.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            service.BlockUserAsync(callerId, targetId));

        Assert.Equal("AdminAccountsSuperAdminOnly", exception.Message);
    }

    [Fact]
    public async Task BlockUserAsync_ValidRequest_BlocksUserAndRevokesTokens()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext();
        var mockUserManager = MockUserManager();

        var callerId = "superadmin-id";
        var targetId = "customer-id";

        var callerUser = new ApplicationUser { Id = callerId };
        var targetUser = new ApplicationUser { Id = targetId, Email = "customer@amorabank.az" };

        mockUserManager.Setup(m => m.FindByIdAsync(targetId)).ReturnsAsync(targetUser);
        mockUserManager.Setup(m => m.FindByIdAsync(callerId)).ReturnsAsync(callerUser);

        // Rolları təyin edirik (SuperAdmin normal müştərini bloklayır)
        mockUserManager.Setup(m => m.GetRolesAsync(targetUser)).ReturnsAsync(new List<string> { "Customer" });
        mockUserManager.Setup(m => m.GetRolesAsync(callerUser)).ReturnsAsync(new List<string> { "SuperAdmin" });

        // Həmin bloklanacaq istifadəçiyə ait bazada 2 dənə aktiv Refresh Token yaradırıq
        dbContext.RefreshTokens.AddRange(
            new RefreshToken { Id = Guid.NewGuid(), UserId = targetId, IsRevoked = false, Token = "token-1" },
            new RefreshToken { Id = Guid.NewGuid(), UserId = targetId, IsRevoked = false, Token = "token-2" }
        );
        await dbContext.SaveChangesAsync();

        var service = new AdminService(
            mockUserManager.Object, null!, dbContext, _mockNotification.Object, _mockLocalizer.Object);

        // Act (Metodu işə salırıq)
        var result = await service.BlockUserAsync(callerId, targetId);

        // Assert (Nəticələri yoxlayırıq)
        Assert.Contains("bloklandı.", result);

        // Identity tərəfində kilidləmə metodlarının düzgün çağırıldığını yoxlayırıq
        mockUserManager.Verify(m => m.SetLockoutEnabledAsync(targetUser, true), Times.Once);
        mockUserManager.Verify(m => m.SetLockoutEndDateAsync(targetUser, It.IsAny<DateTimeOffset>()), Times.Once);

        // Ən kritik hissə: Həmin istifadəçinin bütün aktiv tokenlərinin ləğv edildiyini yoxlayırıq
        var tokens = await dbContext.RefreshTokens.Where(rt => rt.UserId == targetId).ToListAsync();
        Assert.All(tokens, t => Assert.True(t.IsRevoked));
    }
}