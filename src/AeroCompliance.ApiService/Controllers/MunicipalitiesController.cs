using AeroCompliance.ApiService.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AeroCompliance.ApiService.Controllers;

/// <summary>Municipality management. Epic 1: route + authorization stubs only.</summary>
[ApiController]
[Route("api/municipalities")]
[Authorize]
public class MunicipalitiesController : ControllerBase
{
    /// <summary>
    /// List municipalities. Admin-only, to demonstrate policy-based RBAC end-to-end
    /// (no token → 401; non-Admin token → 403; Admin token → reaches the action).
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Policies.RequireAdmin)]
    public IActionResult GetAll()
        // Stub: return an empty list so an authorized call yields 200 rather than 501.
        => Ok(Array.Empty<object>());

    /// <summary>Get a single municipality.</summary>
    [HttpGet("{id:guid}")]
    public IActionResult GetById(Guid id)
        => StatusCode(StatusCodes.Status501NotImplemented);

    /// <summary>Create a municipality (platform admin only).</summary>
    [HttpPost]
    [Authorize(Policy = Policies.ManageMunicipalities)]
    public IActionResult Create()
        => StatusCode(StatusCodes.Status501NotImplemented);

    /// <summary>Update a municipality (platform admin only).</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.ManageMunicipalities)]
    public IActionResult Update(Guid id)
        => StatusCode(StatusCodes.Status501NotImplemented);
}
