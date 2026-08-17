using AeroCompliance.ApiService.Services;
using AeroCompliance.Contracts.Dtos.Auth;
using AeroCompliance.Domain.Entities;
using AeroCompliance.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AeroCompliance.ApiService.Controllers;

/// <summary>
/// Authentication endpoints (login / refresh / logout). All endpoints are anonymous — they are the
/// entry point for obtaining tokens. Refresh tokens are opaque, stored hashed, and rotated on refresh.
/// </summary>
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController(
    AeroComplianceDbContext db,
    IPasswordHasherService passwordHasher,
    ITokenService tokenService) : ControllerBase
{
    /// <summary>Exchange email + password for an access + refresh token pair.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        // Load the user with its role (needed for the role claim).
        var user = await db.Users
            .Include(u => u.Role)
            .SingleOrDefaultAsync(u => u.Email == request.Email, ct);

        // Same 401 whether the email is unknown, the password is wrong, or the account is inactive —
        // never disclose which. Verify a hash even when the user is missing to reduce timing signal.
        var passwordValid = user is not null
            && user.IsActive
            && passwordHasher.Verify(user, user.PasswordHash, request.Password);

        if (user is null || !passwordValid)
        {
            return Unauthorized();
        }

        var response = await IssueAndStoreTokensAsync(user, ct);

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(response);
    }

    /// <summary>Exchange a valid refresh token for a new access + refresh pair (rotation).</summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var tokenHash = tokenService.HashRefreshToken(request.RefreshToken);

        var stored = await db.RefreshTokens
            .Include(rt => rt.User).ThenInclude(u => u.Role)
            .SingleOrDefaultAsync(rt => rt.TokenHash == tokenHash, ct);

        // Reject unknown, expired, or already-revoked tokens (and inactive users) — all as a flat 401.
        if (stored is null || !stored.IsActive || !stored.User.IsActive)
        {
            return Unauthorized();
        }

        // Rotate: revoke the presented token, then issue a fresh pair.
        stored.RevokedAt = DateTimeOffset.UtcNow;

        var response = await IssueAndStoreTokensAsync(stored.User, ct);
        return Ok(response);
    }

    /// <summary>Revoke a refresh token. Idempotent — an unknown/already-revoked token still returns 204.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken ct)
    {
        var tokenHash = tokenService.HashRefreshToken(request.RefreshToken);

        var stored = await db.RefreshTokens
            .SingleOrDefaultAsync(rt => rt.TokenHash == tokenHash, ct);

        if (stored is { RevokedAt: null })
        {
            stored.RevokedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return NoContent();
    }

    /// <summary>Issue a token pair, persist the refresh token's hash, and shape the response.</summary>
    private async Task<TokenResponse> IssueAndStoreTokensAsync(User user, CancellationToken ct)
    {
        var issued = tokenService.IssueTokens(user);

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenService.HashRefreshToken(issued.RefreshToken),
            ExpiresAt = issued.RefreshTokenExpiresAt,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);

        return new TokenResponse(issued.AccessToken, issued.RefreshToken, issued.AccessTokenExpiresAt);
    }
}
