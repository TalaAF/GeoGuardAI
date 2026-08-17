namespace AeroCompliance.Domain.Entities;

/// <summary>A granular permission code (e.g. <c>violation.confirm</c>). Maps to <c>permissions</c>.</summary>
public class Permission
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string? Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
