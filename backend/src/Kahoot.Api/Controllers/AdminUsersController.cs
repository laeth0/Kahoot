using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Admin.Users.GetUserById;
using Kahoot.Application.Features.Admin.Users.ListUsers;
using Kahoot.Application.Features.Admin.Users.ReactivateUser;
using Kahoot.Application.Features.Admin.Users.SuspendUser;
using Kahoot.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kahoot.Api.Controllers;

// System Admin User Management Controller - Provides administrative oversight over Host accounts, keyset-paginated discovery, and suspension lifecycles.
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

    // List Users Endpoint - Returns keyset-paginated list of registered users filtered by status and username prefix.
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

    // Get User By Id Endpoint - Retrieves complete administrative profile for a specific host account.
    [HttpGet("{accountId:guid}")]
    [ProducesResponseType(typeof(UserAdminResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserById(
        [FromRoute] Guid accountId,
        CancellationToken cancellationToken = default)
    {
        GetUserByIdQuery query = new GetUserByIdQuery(accountId);
        Result<UserAdminResponse> result = await _sender.Send(query, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return Problem(result.Error);
    }

    // Suspend User Endpoint - Suspends host account, revokes refresh tokens, bumps TokenSecurityVersion, and enqueues async game teardown.
    [HttpPost("{accountId:guid}/suspend")]
    [ProducesResponseType(typeof(SuspendUserResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SuspendUser(
        [FromRoute] Guid accountId,
        [FromBody] SuspendUserRequest request,
        CancellationToken cancellationToken = default)
    {
        SuspendUserCommand command = new SuspendUserCommand(accountId, request.Revision);
        Result<SuspendUserResult> result = await _sender.Send(command, cancellationToken);

        if (result.IsSuccess)
        {
            if (result.Value.TerminationPending)
            {
                return Accepted(new SuspendUserResponse(TerminationPending: true));
            }

            return NoContent();
        }

        return Problem(result.Error);
    }

    // Reactivate User Endpoint - Restores a suspended user account back to Active status with OCC revision check.
    [HttpPost("{accountId:guid}/reactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReactivateUser(
        [FromRoute] Guid accountId,
        [FromBody] ReactivateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        ReactivateUserCommand command = new ReactivateUserCommand(accountId, request.Revision);
        Result result = await _sender.Send(command, cancellationToken);

        if (result.IsSuccess)
        {
            return NoContent();
        }

        return Problem(result.Error);
    }
}
