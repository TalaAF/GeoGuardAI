namespace AeroCompliance.Contracts.Dtos.Auth;

/// <summary>Credentials submitted to obtain an access + refresh token pair.</summary>
public record LoginRequest(string Email, string Password);

/// <summary>Request to exchange a refresh token for a new access token (token rotation).</summary>
public record RefreshTokenRequest(string RefreshToken);

/// <summary>Request to revoke a refresh token.</summary>
public record LogoutRequest(string RefreshToken);

/// <summary>Token pair returned by login / refresh.</summary>
public record TokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt);
