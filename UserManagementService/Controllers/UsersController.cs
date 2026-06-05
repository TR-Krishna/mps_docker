using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UserManagementService.Controllers;

[ApiController]
[Route("api/ums/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IConfiguration _cfg;
    public UsersController(IConfiguration cfg) => _cfg = cfg;

    /// <summary>List all users (utility_admin only).</summary>
    [HttpGet]
    [Authorize(Roles = "utility_admin")]
    public IActionResult GetAll()
    {
        var users = _cfg.GetSection("MockUsers").Get<List<MockUser>>() ?? new();
        return Ok(users.Select(u => new { u.Id, u.Username, u.Role, u.DisplayName, u.Organisation }));
    }

    /// <summary>Get a user by ID.</summary>
    [HttpGet("{id}")]
    [Authorize(Roles = "utility_admin,field_engineer")]
    public IActionResult GetById(string id)
    {
        var users = _cfg.GetSection("MockUsers").Get<List<MockUser>>() ?? new();
        var u = users.FirstOrDefault(x => x.Id == id);
        return u is null ? NotFound(new { message = $"User '{id}' not found." })
            : Ok(new { u.Id, u.Username, u.Role, u.DisplayName, u.Organisation });
    }
}