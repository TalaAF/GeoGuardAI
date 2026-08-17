namespace AeroCompliance.Domain.Entities;

/// <summary>Join entity for the many-to-many between <see cref="Role"/> and <see cref="Permission"/>.
/// Maps to <c>role_permissions</c> (composite PK role_id + permission_id).</summary>
public class RolePermission
{
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public int PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}
