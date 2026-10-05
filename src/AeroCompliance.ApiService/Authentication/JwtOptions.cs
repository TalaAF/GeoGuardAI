namespace AeroCompliance.ApiService.Authentication;

/// <summary>
/// Bound from the <c>Jwt</c> configuration section. Signing key is a dev default in appsettings;
/// in real environments it comes from user-secrets / a secret store.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// Dev-only fallback signing key used when none is configured, so the local host boots without
    /// requiring every teammate to configure user-secrets first.
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

    /// <summary>
    /// Resolves and validates the effective signing key. The fallback is never allowed outside
    /// Development; production-like environments must provide their own key.
    /// </summary>
    public string ResolveSigningKey(bool allowDevelopmentFallback)
    {
        var key = SigningKey?.Trim();

        if (string.IsNullOrWhiteSpace(key))
        {
            if (allowDevelopmentFallback)
            {
                return DevFallbackSigningKey;
            }

            throw new InvalidOperationException(
                "JWT configuration is invalid: Jwt:SigningKey is required outside Development.");
        }

        if (!allowDevelopmentFallback && key == DevFallbackSigningKey)
        {
            throw new InvalidOperationException(
                "JWT configuration is invalid: the development fallback signing key cannot be used outside Development.");
        }

        if (System.Text.Encoding.UTF8.GetByteCount(key) < 32)
        {
            throw new InvalidOperationException(
                "JWT configuration is invalid: Jwt:SigningKey must contain at least 32 UTF-8 bytes.");
        }

        return key;
    }
}
