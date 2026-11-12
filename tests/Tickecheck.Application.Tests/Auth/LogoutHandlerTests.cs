using Moq;
using Tickecheck.Application.Abstractions;
using Tickecheck.Application.Auth;
using Tickecheck.Domain.Users;

namespace Tickecheck.Application.Tests.Auth;

public class LogoutHandlerTests
{
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly LogoutHandler _handler;

    public LogoutHandlerTests()
    {
        _tokenService.Setup(service => service.HashRefreshToken(It.IsAny<string>()))
            .Returns((string token) => $"hash:{token}");
        _handler = new LogoutHandler(_refreshTokens.Object, _tokenService.Object, _unitOfWork.Object, TestSupport.Clock());
    }

    [Fact]
    public async Task Known_token_is_revoked()
    {
        var token = new RefreshToken(Guid.NewGuid(), "hash:my-token", TestSupport.Now.AddDays(1), TestSupport.Now);
        _refreshTokens.Setup(repo => repo.FindByHashAsync("hash:my-token", default)).ReturnsAsync(token);

        await _handler.HandleAsync(new LogoutCommand("my-token"));

        Assert.True(token.IsRevoked);
        Assert.Equal(TestSupport.Now, token.RevokedAt);
        _unitOfWork.Verify(unit => unit.SaveChangesAsync(default), Times.Once);
    }

    [Theory]
    [InlineData("unknown-token")]
    [InlineData("")]
    public async Task Unknown_or_empty_token_is_ignored_without_error(string refreshToken)
    {
        await _handler.HandleAsync(new LogoutCommand(refreshToken));

        _unitOfWork.Verify(unit => unit.SaveChangesAsync(default), Times.Never);
    }
}
