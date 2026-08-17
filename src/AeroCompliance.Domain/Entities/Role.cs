namespace AeroCompliance.Domain.Entities;

/// <summary>RBAC role. Maps to the <c>roles</c> table (SERIAL PK).</summary>
public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
