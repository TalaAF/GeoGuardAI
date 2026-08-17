using AeroCompliance.ApiService.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AeroCompliance.ApiService.Controllers;

/// <summary>User management. Epic 1: route + authorization stubs only.</summary>
[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    /// <summary>List users (admins / municipality managers only).</summary>
    [HttpGet]
    [Authorize(Policy = Policies.ManageUsers)]
    public IActionResult GetAll()
        => StatusCode(StatusCodes.Status501NotImplemented);

    /// <summary>Get a single user.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.ManageUsers)]
    public IActionResult GetById(Guid id)
        => StatusCode(StatusCodes.Status501NotImplemented);

    /// <summary>Create a user.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.ManageUsers)]
    public IActionResult Create()
        => StatusCode(StatusCodes.Status501NotImplemented);

    /// <summary>Return the currently authenticated user's profile.</summary>
    [HttpGet("me")]
    public IActionResult Me()
        => StatusCode(StatusCodes.Status501NotImplemented);
}
