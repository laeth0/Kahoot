using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Admin.Administrators;
using Kahoot.Application.Features.Admin.Administrators.CreateAdministrator;
using Kahoot.Application.Features.Admin.Administrators.ListAdministrators;
using Kahoot.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kahoot.Api.Controllers;

[ApiController]
[Route("api/admin/administrators")]
[Authorize(Roles = nameof(UserRole.SystemAdmin))]
public sealed class AdminAdministratorsController : ApiController
{
    private readonly ISender _sender;

    public AdminAdministratorsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AdministratorResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAdministrators(CancellationToken cancellationToken = default)
    {
        ListAdministratorsQuery query = new ListAdministratorsQuery();
        Result<IReadOnlyList<AdministratorResponse>> result = await _sender.Send(query, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return Problem(result.Error);
    }

    [HttpPost]
    [ProducesResponseType(typeof(AdministratorResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAdministrator(
        [FromBody] CreateAdministratorRequest request,
        CancellationToken cancellationToken = default)
    {
        CreateAdministratorCommand command = new CreateAdministratorCommand(request.Username, request.Password);
        Result<AdministratorResponse> result = await _sender.Send(command, cancellationToken);

        if (result.IsSuccess)
        {
            return Created(string.Empty, result.Value);
        }

        return Problem(result.Error);
    }
}
