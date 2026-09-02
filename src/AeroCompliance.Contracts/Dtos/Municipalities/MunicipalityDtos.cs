using System.ComponentModel.DataAnnotations;

namespace AeroCompliance.Contracts.Dtos.Municipalities;

public sealed record MunicipalityDto(Guid Id, string Name, string? Region, DateOnly? MouSignedAt,
    bool ApiAccessEnabled, bool IsPilot, DateTimeOffset CreatedAt);

public sealed class SaveMunicipalityRequest
{
    [Required, StringLength(200)] public string Name { get; init; } = string.Empty;
    [StringLength(100)] public string? Region { get; init; }
    public DateOnly? MouSignedAt { get; init; }
    public bool ApiAccessEnabled { get; init; }
    public bool IsPilot { get; init; }
}
