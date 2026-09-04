namespace AeroCompliance.ApiService.Authorization;

/// <summary>
/// Role name constants used in JWT <c>role</c> claims and <c>[Authorize(Roles = ...)]</c> /
/// policy definitions. These mirror <see cref="AeroCompliance.Domain.Enums.RoleName"/> and the
/// seed rows in the <c>roles</c> table.
/// </summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string MunicipalityManager = "MunicipalityManager";
    public const string Inspector = "Inspector";
    public const string GISAnalyst = "GISAnalyst";
    public const string Reviewer = "Reviewer";
    public const string DroneOperator = "DroneOperator";

    public static readonly string[] All =
    [
        Admin, MunicipalityManager, Inspector, GISAnalyst, Reviewer, DroneOperator
    ];
}

/// <summary>
/// Named authorization policy keys. Policies are defined centrally in
/// <see cref="AuthorizationConfiguration"/> so controllers reference a stable name.
/// </summary>
public static class Policies
{
    public const string RequireAdmin = "RequireAdmin";
    public const string ManageUsers = "ManageUsers";
    public const string ManageMunicipalities = "ManageMunicipalities";
    public const string ReviewViolations = "ReviewViolations";
    public const string OperateDrone = "OperateDrone";
    public const string AnalyzeGis = "AnalyzeGis";
    public const string ManageGis = "ManageGis";
    public const string ManageFlights = "ManageFlights";

    /// <summary>Read operational analytics. Excludes DroneOperator, whose remit is flight execution.</summary>
    public const string ViewAnalytics = "ViewAnalytics";

    /// <summary>Read and download generated violation reports.</summary>
    public const string ViewReports = "ViewReports";

    /// <summary>Produce a formal report from a confirmed violation.</summary>
    public const string GenerateReports = "GenerateReports";

    /// <summary>
    /// Create/update a parcel specifically. Narrower than <see cref="ManageGis"/> (which also
    /// covers zones/blocks/roads/zoning rules): parcel records carry cadastral/ownership data, so
    /// editing them is reserved for Admin and MunicipalityManager. GISAnalyst can still read
    /// parcels and edit every other GIS layer via ManageGis, but not parcels.
    /// </summary>
    public const string EditParcels = "EditParcels";
}
