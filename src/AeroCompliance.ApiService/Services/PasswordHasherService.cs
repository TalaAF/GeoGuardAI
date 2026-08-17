using AeroCompliance.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace AeroCompliance.ApiService.Services;

/// <summary>
/// <see cref="IPasswordHasherService"/> backed by <see cref="PasswordHasher{TUser}"/>. Uses the
/// PBKDF2-based algorithm ASP.NET Core Identity ships by default; each <see cref="Hash"/> call
/// produces a distinct salted result.
/// </summary>
public sealed class PasswordHasherService(PasswordHasher<User> hasher) : IPasswordHasherService
{
    public string Hash(User user, string password)
        => hasher.HashPassword(user, password);

    public bool Verify(User user, string passwordHash, string providedPassword)
    {
        var result = hasher.VerifyHashedPassword(user, passwordHash, providedPassword);
        // Treat a "needs rehash" result as valid — the password was correct, just hashed with older params.
        return result is PasswordVerificationResult.Success
            or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
