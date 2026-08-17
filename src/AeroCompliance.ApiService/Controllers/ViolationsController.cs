using AeroCompliance.ApiService.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AeroCompliance.ApiService.Controllers;

/// <summary>Violation review workflow. Epic 1: route + authorization stubs only.</summary>
[ApiController]
[Route("api/violations")]
[Authorize]
public class ViolationsController : ControllerBase
{
    /// <summary>List violations (optionally filtered — later).</summary>
    [HttpGet]
    public IActionResult GetAll()
        => StatusCode(StatusCodes.Status501NotImplemented);

    /// <summary>Get a single violation with its status history.</summary>
    [HttpGet("{id:guid}")]
    public IActionResult GetById(Guid id)
        => StatusCode(StatusCodes.Status501NotImplemented);

    /// <summary>Confirm a violation and advance its status (reviewers / inspectors).</summary>
    [HttpPost("{id:guid}/confirm")]
    [Authorize(Policy = Policies.ReviewViolations)]
    public IActionResult Confirm(Guid id)
        => StatusCode(StatusCodes.Status501NotImplemented);

    /// <summary>Dismiss a violation (reviewers / inspectors).</summary>
    [HttpPost("{id:guid}/dismiss")]
    [Authorize(Policy = Policies.ReviewViolations)]
    public IActionResult Dismiss(Guid id)
        => StatusCode(StatusCodes.Status501NotImplemented);
}
