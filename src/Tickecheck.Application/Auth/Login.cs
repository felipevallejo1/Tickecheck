using FluentValidation;
using Tickecheck.Application.Abstractions;
using Tickecheck.Application.Common;

namespace Tickecheck.Application.Auth;

public record LoginCommand(string Email, string Password);

public class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(command => command.Email).NotEmpty().EmailAddress();
        RuleFor(command => command.Password).NotEmpty();
    }
}

public class LoginHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    AuthTokenIssuer tokenIssuer,
    IUnitOfWork unitOfWork)
{
    public async Task<AuthResponse> HandleAsync(LoginCommand command, CancellationToken cancellationToken = default)
    {
        new LoginValidator().ValidateAndThrow(command);

        var email = command.Email.Trim().ToLowerInvariant();
        var user = await users.FindByEmailIgnoringTenantAsync(email, cancellationToken);

        // Same error for "unknown email" and "wrong password", so the response
        // does not reveal which emails are registered.
        if (user is null || !passwordHasher.Verify(user.PasswordHash, command.Password))
        {
            throw new UnauthorizedException("Invalid email or password.");
        }

        var (response, _) = tokenIssuer.Issue(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return response;
    }
}
