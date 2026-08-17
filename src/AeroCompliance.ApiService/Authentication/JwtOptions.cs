namespace AeroCompliance.ApiService.Authentication;

/// <summary>
/// Bound from the <c>Jwt</c> configuration section. Signing key is a dev default in appsettings;
/// in real environments it comes from user-secrets / a secret store.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// Dev-only fallback signing key used when none is configured, so the host boots without secrets.
    /// Both the JWT validator (<see cref="AuthenticationConfiguration"/>) and the token issuer
    /// (<c>TokenService</c>) must resolve the *same* key, so it lives here as the single source of truth.
    /// Never rely on this in production — configure <c>Jwt:SigningKey</c> via user-secrets / a secret store.
    /// </summary>
    public const string DevFallbackSigningKey = "dev-only-insecure-signing-key-change-me-please-32bytes+";

    public string Issuer { get; set; } = "AeroCompliance";
    public string Audience { get; set; } = "AeroCompliance.Clients";
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Access-token lifetime in minutes.</summary>
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>Refresh-token lifetime in days.</summary>
    public int RefreshTokenDays { get; set; } = 7;

    /// <summary>The effective signing key: the configured one, or the dev fallback when unset.</summary>
    public string ResolveSigningKey()
        => string.IsNullOrWhiteSpace(SigningKey) ? DevFallbackSigningKey : SigningKey;
}
