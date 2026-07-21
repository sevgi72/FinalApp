using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BankApiApp.Data;
using BankApiApp.DTOs.CashByCode;
using BankApiApp.Exceptions;
using BankApiApp.Models;
using BankApiApp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BankApiApp.Tests.Services
{
    public class CashByCodeServiceTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly Mock<ILimitService> _limitServiceMock;
        private readonly Mock<ILocalizationService> _localizerMock;
        private readonly Mock<ILogger<CashByCodeService>> _loggerMock;
        private readonly CashByCodeService _service;

        // Testlərdə istifadə edəcəyimiz sabit Guid-lər
        private readonly Guid _account1Id = Guid.NewGuid();
        private readonly Guid _account2Id = Guid.NewGuid();

        public CashByCodeServiceTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);

            _limitServiceMock = new Mock<ILimitService>();
            _localizerMock = new Mock<ILocalizationService>();
            _loggerMock = new Mock<ILogger<CashByCodeService>>();

            _localizerMock.Setup(l => l.Get(It.IsAny<string>()))
                          .Returns((string key) => key);

            _service = new CashByCodeService(
                _context,
                _limitServiceMock.Object,
                _localizerMock.Object,
                _loggerMock.Object
            );
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        #region CreateAsync Tests

        [Fact]
        public async Task CreateAsync_ValidRequest_DeductsBalanceAndCreatesCashByCode()
        {
            // Arrange
            var userId = "user-1";
            var account = new Account { Id = _account1Id, OwnerId = userId, Balance = 500, Status = AccountStatus.Active };
            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();

            var dto = new CreateCashByCodeDto { AccountId = _account1Id, Amount = 100 };

            // Act
            var result = await _service.CreateAsync(userId, dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(100, result.Amount);
            Assert.Equal("Pending", result.Status);
            Assert.Equal(400, account.Balance);

            var createdCash = await _context.CashByCodes.FirstOrDefaultAsync(c => c.Id == result.Id);
            Assert.NotNull(createdCash);
            Assert.Equal(userId, createdCash.SenderUserId);

            var transaction = await _context.Transactions.FirstOrDefaultAsync(t => t.FromAccountId == _account1Id);
            Assert.NotNull(transaction);
            Assert.Equal(100, transaction.Amount);
        }

        [Fact]
        public async Task CreateAsync_InsufficientBalance_ThrowsBadRequestException()
        {
            // Arrange
            var userId = "user-1";
            var account = new Account { Id = _account1Id, OwnerId = userId, Balance = 30, Status = AccountStatus.Active };
            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();

            var dto = new CreateCashByCodeDto { AccountId = _account1Id, Amount = 100 };

            // Act & Assert
            await Assert.ThrowsAsync<BadRequestException>(() => _service.CreateAsync(userId, dto));
        }

        #endregion

        #region ClaimAsync Tests

        [Fact]
        public async Task ClaimAsync_ValidCode_IncreasesRecipientBalanceAndSetsClaimedStatus()
        {
            // Arrange
            var senderUserId = "user-sender";
            var recipientUserId = "user-recipient";

            var recipientAccount = new Account { Id = _account2Id, OwnerId = recipientUserId, Balance = 50, Status = AccountStatus.Active };
            _context.Accounts.Add(recipientAccount);

            var cash = new CashByCode
            {
                Id = Guid.NewGuid(),
                Code = "12345678",
                Amount = 100,
                SenderUserId = senderUserId,
                SenderAccountId = _account1Id,
                Status = "Pending",
                ExpiresAt = DateTime.UtcNow.AddHours(2)
            };
            _context.CashByCodes.Add(cash);
            await _context.SaveChangesAsync();

            var claimDto = new ClaimCashByCodeDto { Code = "12345678", AccountId = _account2Id };

            // Act
            var resultMessage = await _service.ClaimAsync(recipientUserId, claimDto);

            // Assert
            Assert.Contains("100.00 AZN", resultMessage);
            Assert.Equal(150, recipientAccount.Balance);
            Assert.Equal("Claimed", cash.Status);
            Assert.Equal(recipientUserId, cash.ClaimedByUserId);
            Assert.NotNull(cash.ClaimedAt);
        }

        [Fact]
        public async Task ClaimAsync_ExpiredOrInvalidCode_ThrowsBadRequestException()
        {
            // Arrange
            var recipientUserId = "user-recipient";
            var cash = new CashByCode
            {
                Id = Guid.NewGuid(),
                Code = "87654321",
                Amount = 50,
                SenderUserId = "sender",
                SenderAccountId = _account1Id,
                Status = "Pending",
                ExpiresAt = DateTime.UtcNow.AddHours(-1)
            };
            _context.CashByCodes.Add(cash);
            await _context.SaveChangesAsync();

            var claimDto = new ClaimCashByCodeDto { Code = "87654321", AccountId = _account2Id };

            // Act & Assert
            await Assert.ThrowsAsync<BadRequestException>(() => _service.ClaimAsync(recipientUserId, claimDto));
        }

        #endregion

        #region CancelAsync Tests

        [Fact]
        public async Task CancelAsync_PendingCode_RefundsSenderAndCancelsCode()
        {
            // Arrange
            var userId = "user-sender";
            var account = new Account { Id = _account1Id, OwnerId = userId, Balance = 200, Status = AccountStatus.Active };
            _context.Accounts.Add(account);

            var cash = new CashByCode
            {
                Id = Guid.NewGuid(),
                Code = "11223344",
                Amount = 100,
                SenderUserId = userId,
                SenderAccountId = _account1Id,
                Status = "Pending",
                ExpiresAt = DateTime.UtcNow.AddHours(12)
            };
            _context.CashByCodes.Add(cash);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.CancelAsync(userId, cash.Id);

            // Assert
            Assert.Equal("Cancelled", cash.Status);
            Assert.Equal(300, account.Balance);
        }

        #endregion

        #region ExpireOldCodesAsync Tests

        [Fact]
        public async Task ExpireOldCodesAsync_ExpiredPendingCodes_RefundsMoneyAndUpdatesStatusToExpired()
        {
            // Arrange
            var account = new Account { Id = _account1Id, OwnerId = "user-1", Balance = 100, Status = AccountStatus.Active };
            _context.Accounts.Add(account);

            var expiredCash = new CashByCode
            {
                Id = Guid.NewGuid(),
                Code = "99887766",
                Amount = 150,
                SenderUserId = "user-1",
                SenderAccountId = _account1Id,
                Status = "Pending",
                ExpiresAt = DateTime.UtcNow.AddHours(-5)
            };
            _context.CashByCodes.Add(expiredCash);
            await _context.SaveChangesAsync();

            // Act
            await _service.ExpireOldCodesAsync();

            // Assert
            Assert.Equal("Expired", expiredCash.Status);
            Assert.Equal(250, account.Balance);

            var refundTransaction = await _context.Transactions.FirstOrDefaultAsync(t => t.ToAccountId == _account1Id);
            Assert.NotNull(refundTransaction);
            Assert.Equal(TransactionType.Deposit, refundTransaction.Type);
        }

        #endregion
    }
}