using Moq;
using Tickecheck.Application.Abstractions;
using Tickecheck.Application.Auth;
using Tickecheck.Application.Common;
using Tickecheck.Domain.Users;

namespace Tickecheck.Application.Tests.Auth;

public class RefreshSessionHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly User _user = TestSupport.NewUser(UserRole.Agent);
    private readonly RefreshSessionHandler _handler;

    public RefreshSessionHandlerTests()
    {
        // The fake hash is just "hash:" + token, which keeps the tests easy to follow.
        _tokenService.Setup(service => service.HashRefreshToken(It.IsAny<string>()))
            .Returns((string token) => $"hash:{token}");
        _tokenService.Setup(service => service.GenerateRefreshToken()).Returns("new-token");
        _tokenService.Setup(service => service.RefreshTokenLifetime).Returns(TimeSpan.FromDays(7));
        _tokenService.Setup(service => service.CreateAccessToken(It.IsAny<User>()))
            .Returns(new AccessToken("new-access-token", TestSupport.Now.AddMinutes(15)));
        _users.Setup(repo => repo.GetByIdIgnoringTenantAsync(_user.Id, default)).ReturnsAsync(_user);

        var clock = TestSupport.Clock();
        var issuer = new AuthTokenIssuer(_tokenService.Object, _refreshTokens.Object, clock);
        _handler = new RefreshSessionHandler(
            _refreshTokens.Object, _users.Object, _tokenService.Object, issuer, _unitOfWork.Object, clock);
    }

    [Fact]
    public async Task Valid_token_is_rotated_the_old_one_is_revoked_and_points_to_the_new_one()
    {
        var stored = StoreToken("old-token", expiresAt: TestSupport.Now.AddDays(1));

        var response = await _handler.HandleAsync(new RefreshSessionCommand("old-token"));

        Assert.Equal("new-access-token", response.AccessToken);
        Assert.Equal("new-token", response.RefreshToken);
        Assert.True(stored.IsRevoked);
        Assert.Equal("hash:new-token", stored.ReplacedByTokenHash);
        _refreshTokens.Verify(repo => repo.Add(It.Is<RefreshToken>(token => token.TokenHash == "hash:new-token")), Times.Once);
        _unitOfWork.Verify(unit => unit.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Unknown_token_is_rejected()
    {
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _handler.HandleAsync(new RefreshSessionCommand("never-issued")));
    }

    [Fact]
    public async Task Expired_token_is_rejected()
    {
        StoreToken("old-token", expiresAt: TestSupport.Now.AddSeconds(-1));

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _handler.HandleAsync(new RefreshSessionCommand("old-token")));

        _refreshTokens.Verify(repo => repo.Add(It.IsAny<RefreshToken>()), Times.Never);
    }

    [Fact]
    public async Task Reusing_a_revoked_token_revokes_every_active_session_of_the_user()
    {
        var stolen = StoreToken("old-token", expiresAt: TestSupport.Now.AddDays(1));
        stolen.Revoke(TestSupport.Now.AddHours(-1), "hash:current-token");
        var current = new RefreshToken(_user.Id, "hash:current-token", TestSupport.Now.AddDays(1), TestSupport.Now);
        _refreshTokens.Setup(repo => repo.ListActiveByUserAsync(_user.Id, TestSupport.Now, default))
            .ReturnsAsync([current]);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _handler.HandleAsync(new RefreshSessionCommand("old-token")));

        Assert.True(current.IsRevoked);
        _unitOfWork.Verify(unit => unit.SaveChangesAsync(default), Times.Once);
        _refreshTokens.Verify(repo => repo.Add(It.IsAny<RefreshToken>()), Times.Never);
    }

    [Fact]
    public async Task Token_of_a_user_that_no_longer_exists_is_rejected()
    {
        StoreToken("old-token", expiresAt: TestSupport.Now.AddDays(1));
        _users.Setup(repo => repo.GetByIdIgnoringTenantAsync(_user.Id, default)).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _handler.HandleAsync(new RefreshSessionCommand("old-token")));
    }

    private RefreshToken StoreToken(string rawToken, DateTime expiresAt)
    {
        var token = new RefreshToken(_user.Id, $"hash:{rawToken}", expiresAt, TestSupport.Now.AddDays(-1));
        _refreshTokens.Setup(repo => repo.FindByHashAsync($"hash:{rawToken}", default)).ReturnsAsync(token);
        return token;
    }
}
