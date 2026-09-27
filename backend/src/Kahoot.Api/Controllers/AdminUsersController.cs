using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Admin.Users.ListUsers;
using Kahoot.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kahoot.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = nameof(UserRole.SystemAdmin))]
public sealed class AdminUsersController : ApiController
{
    private readonly ISender _sender;

    public AdminUsersController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ListUsersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListUsers(
        [FromQuery] string? cursor = null,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? username = null,
        [FromQuery] UserStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        ListUsersQuery query = new ListUsersQuery(cursor, pageSize, username, status);
        Result<ListUsersResponse> result = await _sender.Send(query, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return Problem(result.Error);
    }
}
