using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AeroCompliance.ApiService.Authentication;
using AeroCompliance.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AeroCompliance.ApiService.Services;

/// <summary>
/// Default <see cref="ITokenService"/>. Signs access tokens with the same key the JWT validator uses
/// (<see cref="JwtOptions.ResolveSigningKey"/>) and generates cryptographically-random refresh tokens.
/// </summary>
public sealed class TokenService(IOptions<JwtOptions> options, IHostEnvironment environment) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public IssuedTokens IssueTokens(User user)
    {
        var now = DateTimeOffset.UtcNow;
        var accessExpires = now.AddMinutes(_options.AccessTokenMinutes);

        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_options.ResolveSigningKey(environment.IsDevelopment())));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            // ClaimTypes.Role is what RequireRole / [Authorize(Roles=...)] checks by default.
            new(ClaimTypes.Role, user.Role.Name)
        };

        if (user.MunicipalityId is Guid municipalityId)
        {
            claims.Add(new Claim(AeroComplianceClaimTypes.MunicipalityId, municipalityId.ToString()));
        }

        var jwt = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: accessExpires.UtcDateTime,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(jwt);

        var refreshToken = GenerateRefreshToken();
        var refreshExpires = now.AddDays(_options.RefreshTokenDays);

        return new IssuedTokens(accessToken, accessExpires, refreshToken, refreshExpires);
    }

    public string HashRefreshToken(string refreshToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToHexStringLower(bytes);
    }

    /// <summary>256 bits of entropy, URL-safe base64 — opaque to clients.</summary>
    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
