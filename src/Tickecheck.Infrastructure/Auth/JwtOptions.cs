namespace Tickecheck.Infrastructure.Auth;

/// <summary>
/// Settings read from the "Jwt" section of the configuration.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "Tickecheck";
    public string Audience { get; set; } = "Tickecheck";

    // Secret used to sign the tokens. Never committed for production: it comes from
    // an environment variable (Jwt__SigningKey).
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}
