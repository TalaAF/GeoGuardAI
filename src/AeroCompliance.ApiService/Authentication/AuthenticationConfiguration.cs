using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace AeroCompliance.ApiService.Authentication;

/// <summary>
/// Configures JWT Bearer authentication. Epic 1: validation parameters and options binding only;
/// token issuance/refresh logic lives behind the <c>AuthController</c> stubs and is filled in later.
/// </summary>
public static class AuthenticationConfiguration
{
    public static IServiceCollection AddAeroComplianceJwtAuth(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        services.Configure<JwtOptions>(jwtSection);
        var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();

        // Same key the token issuer uses (see JwtOptions.ResolveSigningKey) so validation matches issuance.
        if (string.IsNullOrWhiteSpace(jwtOptions.Issuer)
            || string.IsNullOrWhiteSpace(jwtOptions.Audience)
            || jwtOptions.AccessTokenMinutes <= 0
            || jwtOptions.RefreshTokenDays <= 0)
        {
            throw new InvalidOperationException(
                "JWT configuration is invalid: issuer, audience, and positive token lifetimes are required.");
        }

        var signingKey = jwtOptions.ResolveSigningKey(environment.IsDevelopment());

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        return services;
    }
}
