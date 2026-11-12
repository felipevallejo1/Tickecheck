using FluentValidation;
using Tickecheck.Application.Abstractions;
using Tickecheck.Application.Common;
using Tickecheck.Domain.Users;

namespace Tickecheck.Application.Users;

public record UserDto(Guid Id, string Email, string FullName, UserRole Role);

public record CreateUserCommand(string Email, string FullName, string Password, UserRole Role);

public class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(command => command.FullName).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Password).NotEmpty().MinimumLength(8);

        // A TenantAdmin manages people of the company. Platform admins are not created from here.
        RuleFor(command => command.Role)
            .IsInEnum()
            .NotEqual(UserRole.SuperAdmin)
            .WithMessage("A SuperAdmin cannot be created inside a tenant.");
    }
}

/// <summary>
/// A TenantAdmin adds a user to their own company.
/// </summary>
public class CreateUserHandler(
    IUserRepository users,
    ITenantProvider tenantProvider,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<UserDto> HandleAsync(CreateUserCommand command, CancellationToken cancellationToken = default)
    {
        new CreateUserValidator().ValidateAndThrow(command);

        var email = command.Email.Trim().ToLowerInvariant();
        if (await users.EmailExistsIgnoringTenantAsync(email, cancellationToken))
        {
            throw new ConflictException("The email is already in use.");
        }

        // The tenant comes from the admin's token, never from the request body.
        var user = new User(
            tenantProvider.TenantId,
            email,
            passwordHasher.Hash(command.Password),
            command.FullName,
            command.Role,
            timeProvider.GetUtcNow().UtcDateTime);

        users.Add(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new UserDto(user.Id, user.Email, user.FullName, user.Role);
    }
}
