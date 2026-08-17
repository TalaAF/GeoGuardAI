using AeroCompliance.Domain.Entities;

namespace AeroCompliance.ApiService.Services;

/// <summary>Result of issuing a token pair: the signed access JWT and the plaintext refresh token.</summary>
/// <param name="AccessToken">Signed JWT access token.</param>
/// <param name="AccessTokenExpiresAt">Absolute expiry of the access token.</param>
/// <param name="RefreshToken">Opaque plaintext refresh token — return to the caller once, store only its hash.</param>
/// <param name="RefreshTokenExpiresAt">Absolute expiry of the refresh token.</param>
public record IssuedTokens(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);

/// <summary>
/// Issues authentication tokens: a short-lived access JWT plus a long-lived opaque refresh token.
/// Refresh tokens are random strings (not JWTs) and are persisted hashed by the caller.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Build a signed access JWT (with sub/email/role claims) and a fresh opaque refresh token.
    /// The user's <see cref="User.Role"/> must be loaded so the role claim can be populated.
    /// </summary>
    IssuedTokens IssueTokens(User user);

    /// <summary>Deterministically hash a refresh token for storage/lookup (SHA-256, hex).</summary>
    string HashRefreshToken(string refreshToken);
}
