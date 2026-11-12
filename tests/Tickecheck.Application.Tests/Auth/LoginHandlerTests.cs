using FluentValidation;
using Moq;
using Tickecheck.Application.Abstractions;
using Tickecheck.Application.Auth;
using Tickecheck.Application.Common;
using Tickecheck.Domain.Users;

namespace Tickecheck.Application.Tests.Auth;

public class LoginHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly LoginHandler _handler;

    public LoginHandlerTests()
    {
        _tokenService.Setup(service => service.CreateAccessToken(It.IsAny<User>()))
            .Returns(new AccessToken("access-token", TestSupport.Now.AddMinutes(15)));
        _tokenService.Setup(service => service.GenerateRefreshToken()).Returns("raw-refresh-token");
        _tokenService.Setup(service => service.HashRefreshToken("raw-refresh-token")).Returns("hashed-refresh-token");
        _tokenService.Setup(service => service.RefreshTokenLifetime).Returns(TimeSpan.FromDays(7));

        var issuer = new AuthTokenIssuer(_tokenService.Object, _refreshTokens.Object, TestSupport.Clock());
        _handler = new LoginHandler(_users.Object, _passwordHasher.Object, issuer, _unitOfWork.Object);
    }

    [Fact]
    public async Task Valid_credentials_return_tokens_and_store_only_the_hash_of_the_refresh_token()
    {
        var user = TestSupport.NewUser(UserRole.Agent, "agent@acme.demo");
        _users.Setup(repo => repo.FindByEmailIgnoringTenantAsync("agent@acme.demo", default)).ReturnsAsync(user);
        _passwordHasher.Setup(hasher => hasher.Verify("stored-hash", "secret")).Returns(true);

        var response = await _handler.HandleAsync(new LoginCommand("agent@acme.demo", "secret"));

        Assert.Equal("access-token", response.AccessToken);
        Assert.Equal("raw-refresh-token", response.RefreshToken);
        _refreshTokens.Verify(repo => repo.Add(It.Is<RefreshToken>(token =>
            token.UserId == user.Id
            && token.TokenHash == "hashed-refresh-token"
            && token.ExpiresAt == TestSupport.Now.AddDays(7))), Times.Once);
        _unitOfWork.Verify(unit => unit.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Email_is_normalized_before_the_lookup()
    {
        var user = TestSupport.NewUser(UserRole.Agent, "agent@acme.demo");
        _users.Setup(repo => repo.FindByEmailIgnoringTenantAsync("agent@acme.demo", default)).ReturnsAsync(user);
        _passwordHasher.Setup(hasher => hasher.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var response = await _handler.HandleAsync(new LoginCommand("  Agent@ACME.demo ", "secret"));

        Assert.NotNull(response);
    }

    [Fact]
    public async Task Unknown_email_is_rejected()
    {
        _users.Setup(repo => repo.FindByEmailIgnoringTenantAsync(It.IsAny<string>(), default)).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => _handler.HandleAsync(new LoginCommand("nobody@acme.demo", "secret")));

        _unitOfWork.Verify(unit => unit.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Wrong_password_is_rejected_with_the_same_message_as_an_unknown_email()
    {
        _users.Setup(repo => repo.FindByEmailIgnoringTenantAsync("agent@acme.demo", default))
            .ReturnsAsync(TestSupport.NewUser(UserRole.Agent, "agent@acme.demo"));
        _passwordHasher.Setup(hasher => hasher.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var wrongPassword = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _handler.HandleAsync(new LoginCommand("agent@acme.demo", "wrong")));
        var unknownEmail = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _handler.HandleAsync(new LoginCommand("nobody@acme.demo", "wrong")));

        Assert.Equal(unknownEmail.Message, wrongPassword.Message);
    }

    [Fact]
    public async Task Empty_fields_fail_validation_before_touching_the_database()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _handler.HandleAsync(new LoginCommand("", "")));

        _users.VerifyNoOtherCalls();
    }
}
