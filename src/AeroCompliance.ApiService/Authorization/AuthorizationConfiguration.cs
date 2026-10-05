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
            .AddPolicy(Policies.AnalyzeGis, p => p.RequireRole(Roles.Admin, Roles.GISAnalyst))
            .AddPolicy(Policies.ManageGis, p => p.RequireRole(Roles.Admin, Roles.MunicipalityManager, Roles.GISAnalyst))
            .AddPolicy(Policies.ManageFlights, p => p.RequireRole(Roles.Admin, Roles.MunicipalityManager, Roles.DroneOperator))
            // Phase 6. Analytics and reports are oversight functions: every role with a compliance
            // remit may read them, while DroneOperator (flight execution only) is excluded — the
            // frontend hides the menu entries to match, but this policy is what actually enforces it.
            .AddPolicy(Policies.ViewAnalytics, p => p.RequireRole(
                Roles.Admin, Roles.MunicipalityManager, Roles.Inspector, Roles.Reviewer, Roles.GISAnalyst))
            .AddPolicy(Policies.ViewReports, p => p.RequireRole(
                Roles.Admin, Roles.MunicipalityManager, Roles.Inspector, Roles.Reviewer, Roles.GISAnalyst))
            // Generating a formal report is a review action, so it tracks the reviewer roles rather
            // than the wider read audience (GISAnalyst and MunicipalityManager read only).
            .AddPolicy(Policies.GenerateReports, p => p.RequireRole(Roles.Admin, Roles.Inspector, Roles.Reviewer))
            // Parcel mutation only: narrower than ManageGis because parcels carry cadastral/
            // ownership data. GISAnalyst keeps ManageGis for zones/blocks/roads/zoning rules and
            // keeps read access to parcels, but not parcel mutation. This must exactly match the
            // frontend's `gis.parcels.edit` capability — the frontend hiding a control is a UX
            // nicety, this policy is what actually stops the request.
            .AddPolicy(Policies.EditParcels, p => p.RequireRole(Roles.Admin, Roles.MunicipalityManager));

        return services;
    }
}
