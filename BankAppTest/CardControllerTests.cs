using BankApiApp.Controllers;
using BankApiApp.DTOs.Card;
using BankApiApp.DTOs.Common;
using BankApiApp.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;
using Xunit;

namespace BankApiApp.Tests.Controllers;

public class CardControllerTests
{
    private readonly Mock<ICardService> _cardServiceMock;
    private readonly CardController _controller;
    private readonly string _testUserId;

    public CardControllerTests()
    {
        _cardServiceMock = new Mock<ICardService>();
        _controller = new CardController(_cardServiceMock.Object);

        // Test üçün saxta bir User ID təyin edirik
        _testUserId = Guid.NewGuid().ToString();

        // User.FindFirstValue(ClaimTypes.NameIdentifier) hissəsinin işləməsi üçün Mock User qururuq
        var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, _testUserId) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    [Fact]
    public async Task Create_ShouldReturnOk_WithCardCreatedDto()
    {
        // Arrange
        var dto = new CreateCardDto { AccountId = Guid.NewGuid() };
        var expectedResponse = new CardCreatedDto
        {
            Id = Guid.NewGuid(),
            CardNumber = "4169730123456789",
            Cvv = "123",
            CardHolderName = "John Doe",
            Expiry = "07/31",
            Status = "Active"
        };

        _cardServiceMock
            .Setup(s => s.CreateCardAsync(_testUserId, dto))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.Create(dto);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(expectedResponse);
    }

    [Fact]
    public async Task My_ShouldReturnOk_WithPagedCardDtoList()
    {
        // Arrange
        var pagination = new PaginationParams { Page = 1, PageSize = 10 };
        var expectedCards = new PagedResult<CardDto>
        {
            Items = new List<CardDto>
            {
                new CardDto
                {
                    Id = Guid.NewGuid(),
                    MaskedNumber = "416973******6789",
                    CardHolderName = "John Doe",
                    Expiry = "07/31",
                    Status = "Active",
                    AccountId = Guid.NewGuid()
                }
            }
        };

        _cardServiceMock
            .Setup(s => s.GetMyCardsAsync(_testUserId, pagination))
            .ReturnsAsync(expectedCards);

        // Act
        var result = await _controller.My(pagination);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(expectedCards);
    }

    [Fact]
    public async Task Get_ShouldReturnOk_WithCardDtoDetails()
    {
        // Arrange
        var cardId = Guid.NewGuid();
        var expectedCard = new CardDto
        {
            Id = cardId,
            MaskedNumber = "416973******6789",
            CardHolderName = "John Doe",
            Expiry = "07/31",
            Status = "Active",
            AccountId = Guid.NewGuid()
        };

        _cardServiceMock
            .Setup(s => s.GetCardAsync(_testUserId, cardId))
            .ReturnsAsync(expectedCard);

        // Act
        var result = await _controller.Get(cardId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(expectedCard);
    }

    [Fact]
    public async Task Block_ShouldReturnOk_WithMessage()
    {
        // Arrange
        var cardId = Guid.NewGuid();
        _cardServiceMock
            .Setup(s => s.BlockCardAsync(_testUserId, cardId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Block(cardId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(new { message = "Kart bloklandi." });
        _cardServiceMock.Verify(s => s.BlockCardAsync(_testUserId, cardId), Times.Once);
    }

    [Fact]
    public async Task Unblock_ShouldReturnOk_WithMessage()
    {
        // Arrange
        var cardId = Guid.NewGuid();
        _cardServiceMock
            .Setup(s => s.UnblockCardAsync(_testUserId, cardId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Unblock(cardId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(new { message = "Kart aktivlesdirildi." });
        _cardServiceMock.Verify(s => s.UnblockCardAsync(_testUserId, cardId), Times.Once);
    }
}