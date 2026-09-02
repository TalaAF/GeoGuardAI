namespace AeroCompliance.Contracts.Dtos.Users;

/// <summary>A safe projection of the currently authenticated application user.</summary>
public sealed record CurrentUserDto(
    Guid Id,
    string FullName,
    string Email,
    CurrentUserRoleDto Role,
    CurrentUserMunicipalityDto? Municipality,
    bool IsActive);

/// <summary>Role information exposed in the current-user response.</summary>
public sealed record CurrentUserRoleDto(int Id, string Name);

/// <summary>Municipality information exposed in the current-user response.</summary>
public sealed record CurrentUserMunicipalityDto(Guid Id, string Name);
