using FluentValidation;
using Tickecheck.Application.Abstractions;
using Tickecheck.Application.Common;
using Tickecheck.Domain.Tenants;
using Tickecheck.Domain.Users;

namespace Tickecheck.Application.Tenants;

public record TenantDto(Guid Id, string Name, DateTime CreatedAt);

public record CreateTenantCommand(string Name, string AdminEmail, string AdminFullName, string AdminPassword);

public class CreateTenantValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.AdminEmail).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(command => command.AdminFullName).NotEmpty().MaximumLength(200);
        RuleFor(command => command.AdminPassword).NotEmpty().MinimumLength(8);
    }
}

/// <summary>
/// Creates a company together with its first TenantAdmin. Both are saved in one
/// transaction: a company without an admin could never be used.
/// </summary>
public class CreateTenantHandler(
    ITenantRepository tenants,
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<TenantDto> HandleAsync(CreateTenantCommand command, CancellationToken cancellationToken = default)
    {
        new CreateTenantValidator().ValidateAndThrow(command);

        var email = command.AdminEmail.Trim().ToLowerInvariant();
        if (await users.EmailExistsIgnoringTenantAsync(email, cancellationToken))
        {
            throw new ConflictException("The email is already in use.");
        }

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var tenant = new Tenant(command.Name, utcNow);
        var admin = new User(
            tenant.Id,
            email,
            passwordHasher.Hash(command.AdminPassword),
            command.AdminFullName,
            UserRole.TenantAdmin,
            utcNow);

        tenants.Add(tenant);
        users.Add(admin);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new TenantDto(tenant.Id, tenant.Name, tenant.CreatedAt);
    }
}
