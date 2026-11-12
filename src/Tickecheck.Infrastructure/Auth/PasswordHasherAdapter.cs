using Microsoft.AspNetCore.Identity;
using Tickecheck.Application.Abstractions;
using Tickecheck.Domain.Users;

namespace Tickecheck.Infrastructure.Auth;

/// <summary>
/// Uses only the password hasher of ASP.NET Core Identity (PBKDF2 with a random salt
/// per password), without the rest of Identity: its tables, managers and UI are not needed here.
/// </summary>
public class PasswordHasherAdapter : IPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(string password)
    {
        // The hasher does not use the user argument, so null is safe here.
        return _hasher.HashPassword(null!, password);
    }

    public bool Verify(string passwordHash, string password)
    {
        var result = _hasher.VerifyHashedPassword(null!, passwordHash, password);
        return result != PasswordVerificationResult.Failed;
    }
}
