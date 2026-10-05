using System.ComponentModel.DataAnnotations;

namespace AeroCompliance.Contracts.Dtos.Users;

public sealed record UserDto(Guid Id, string FullName, string Email, CurrentUserRoleDto Role,
    CurrentUserMunicipalityDto? Municipality, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset? LastLoginAt);

public sealed class CreateUserRequest
{
    [Required, StringLength(200)] public string FullName { get; init; } = string.Empty;
    [Required, EmailAddress, StringLength(200)] public string Email { get; init; } = string.Empty;
    [Required, MinLength(8)] public string InitialPassword { get; init; } = string.Empty;
    [Required] public string Role { get; init; } = string.Empty;
    public Guid? MunicipalityId { get; init; }
}

public sealed class UpdateUserRequest
{
    [Required, StringLength(200)] public string FullName { get; init; } = string.Empty;
    [Required, EmailAddress, StringLength(200)] public string Email { get; init; } = string.Empty;
    [Required] public string Role { get; init; } = string.Empty;
    public Guid? MunicipalityId { get; init; }
}

public sealed class UserStateChangeRequest
{
    [StringLength(1000)] public string? Reason { get; init; }
}
