namespace AeroCompliance.Domain.Enums;

/// <summary>
/// Canonical role names used across the platform. These mirror the seed rows in the
/// <c>roles</c> table and are the values referenced by the API's RBAC authorization policies.
/// The <see cref="System.ComponentModel.DescriptionAttribute"/> holds the human-facing name
/// used in the SQL schema comments.
/// </summary>
public enum RoleName
{
    Admin = 1,
    MunicipalityManager = 2,
    Inspector = 3,
    GISAnalyst = 4,
    Reviewer = 5,
    DroneOperator = 6
}
