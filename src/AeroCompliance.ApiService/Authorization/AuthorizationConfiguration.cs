using Microsoft.AspNetCore.Authorization;

namespace AeroCompliance.ApiService.Authorization;

/// <summary>
/// Registers the policy-based RBAC used across the API. Epic 1: policies map to roles only;
/// finer-grained permission checks (from the <c>permissions</c> / <c>role_permissions</c> tables)
/// come in a later epic.
/// </summary>
public static class AuthorizationConfiguration
{
    public static IServiceCollection AddAeroComplianceAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.RequireAdmin, p => p.RequireRole(Roles.Admin))
            .AddPolicy(Policies.ManageUsers, p => p.RequireRole(Roles.Admin, Roles.MunicipalityManager))
            .AddPolicy(Policies.ManageMunicipalities, p => p.RequireRole(Roles.Admin))
            .AddPolicy(Policies.ReviewViolations, p => p.RequireRole(Roles.Admin, Roles.Reviewer, Roles.Inspector))
            .AddPolicy(Policies.OperateDrone, p => p.RequireRole(Roles.Admin, Roles.DroneOperator))
            .AddPolicy(Policies.AnalyzeGis, p => p.RequireRole(Roles.Admin, Roles.GISAnalyst));

        return services;
    }
}
