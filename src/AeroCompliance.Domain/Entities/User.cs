namespace AeroCompliance.Domain.Entities;

/// <summary>An application user. Maps to <c>users</c>.</summary>
public class User
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;

    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    /// <summary>NULL for a platform-wide Admin not scoped to a single municipality.</summary>
    public Guid? MunicipalityId { get; set; }
    public Municipality? Municipality { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
