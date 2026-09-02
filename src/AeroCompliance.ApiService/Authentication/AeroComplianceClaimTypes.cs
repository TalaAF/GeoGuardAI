namespace AeroCompliance.ApiService.Authentication;

/// <summary>Application-specific JWT claim names shared by token issuance and request contexts.</summary>
public static class AeroComplianceClaimTypes
{
    /// <summary>The authenticated user's municipality tenant. Platform Admin tokens omit it.</summary>
    public const string MunicipalityId = "municipality_id";
}
