namespace Kahoot.Api.UnitTests.Features.Admin;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kahoot.Api.Controllers;
using Kahoot.Api.UnitTests.TestSupport;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Admin.Administrators;
using Kahoot.Application.Features.Admin.Administrators.CreateAdministrator;
using Kahoot.Application.Features.Admin.Administrators.ListAdministrators;
using Kahoot.Application.Features.Admin.Administrators.ReactivateAdministrator;
using Kahoot.Application.Features.Admin.Administrators.SuspendAdministrator;
using Kahoot.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

public sealed class AdminAdministratorsControllerTests
{
    private readonly RecordingSender _sender = new();

    private AdminAdministratorsController CreateController(HttpContext httpContext)
    {
        return new AdminAdministratorsController(_sender)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            ProblemDetailsFactory = new TestProblemDetailsFactory()
        };
    }

    [Fact]
    public async Task ListAdministrators_Success_DispatchesQueryAndReturnsOk()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/api/admin/administrators");
        AdminAdministratorsController controller = CreateController(context);
        CancellationTokenSource cts = new();

        IReadOnlyList<AdministratorResponse> expectedList = new List<AdministratorResponse>
        {
            new(Guid.NewGuid(), "admin1", UserRole.SystemAdmin, UserStatus.Active, DateTimeOffset.UtcNow, null, 1, false)
        };

        _sender.RespondWith(Result.Success(expectedList));

        IActionResult result = await controller.ListAdministrators(cts.Token);

        OkObjectResult okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        Assert.Same(expectedList, okResult.Value);

        Assert.Single(_sender.Requests);
        Assert.IsType<ListAdministratorsQuery>(_sender.Requests[0]);
        Assert.Equal(cts.Token, _sender.CancellationTokens[0]);
    }

    [Fact]
    public async Task ListAdministrators_Failure_ReturnsProblemDetails()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/api/admin/administrators");
        AdminAdministratorsController controller = CreateController(context);

        _sender.RespondWith(Result.Failure<IReadOnlyList<AdministratorResponse>>(Error.Forbidden("Admin.Forbidden", "Access denied.")));

        IActionResult result = await controller.ListAdministrators(CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public async Task CreateAdministrator_Success_DispatchesCommandAndReturnsCreated()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/api/admin/administrators");
        AdminAdministratorsController controller = CreateController(context);
        CancellationTokenSource cts = new();

        CreateAdministratorRequest request = new("newadmin", "SuperSecurePassword123!");
        AdministratorResponse expectedResponse = new(
            Guid.NewGuid(),
            request.Username,
            UserRole.SystemAdmin,
            UserStatus.Active,
            DateTimeOffset.UtcNow,
            null,
            1,
            false);

        _sender.RespondWith(Result.Success(expectedResponse));

        IActionResult result = await controller.CreateAdministrator(request, cts.Token);

        CreatedResult created = Assert.IsType<CreatedResult>(result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Equal(string.Empty, created.Location);
        Assert.Same(expectedResponse, created.Value);

        Assert.Single(_sender.Requests);
        CreateAdministratorCommand command = Assert.IsType<CreateAdministratorCommand>(_sender.Requests[0]);
        Assert.Equal(request.Username, command.Username);
        Assert.Equal(request.Password, command.Password);
        Assert.Equal(cts.Token, _sender.CancellationTokens[0]);
    }

    [Fact]
    public async Task CreateAdministrator_Failure_ReturnsProblemDetails()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/api/admin/administrators");
        AdminAdministratorsController controller = CreateController(context);

        CreateAdministratorRequest request = new("existingadmin", "Password123!");
        _sender.RespondWith(Result.Failure<AdministratorResponse>(Error.Conflict("User.UsernameExists", "Username already exists.")));

        IActionResult result = await controller.CreateAdministrator(request, CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
    }

    [Fact]
    public async Task SuspendAdministrator_Success_DispatchesCommandAndReturnsNoContent()
    {
        Guid adminId = Guid.NewGuid();
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/admin/administrators/{adminId}/suspend");
        AdminAdministratorsController controller = CreateController(context);
        CancellationTokenSource cts = new();

        SuspendAdministratorRequest request = new(Revision: 3);
        _sender.RespondWith(Result.Success());

        IActionResult result = await controller.SuspendAdministrator(adminId, request, cts.Token);

        NoContentResult noContent = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContent.StatusCode);

        Assert.Single(_sender.Requests);
        SuspendAdministratorCommand command = Assert.IsType<SuspendAdministratorCommand>(_sender.Requests[0]);
        Assert.Equal(adminId, command.AdministratorId);
        Assert.Equal(3, command.Revision);
        Assert.Equal(cts.Token, _sender.CancellationTokens[0]);
    }

    [Fact]
    public async Task SuspendAdministrator_LastAdmin_ReturnsProblemDetails()
    {
        Guid adminId = Guid.NewGuid();
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/admin/administrators/{adminId}/suspend");
        AdminAdministratorsController controller = CreateController(context);

        SuspendAdministratorRequest request = new(Revision: 1);
        _sender.RespondWith(Result.Failure(Error.Conflict("Admin.LastActiveAdmin", "Cannot suspend the last active administrator.")));

        IActionResult result = await controller.SuspendAdministrator(adminId, request, CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
    }

    [Fact]
    public async Task ReactivateAdministrator_Success_DispatchesCommandAndReturnsNoContent()
    {
        Guid adminId = Guid.NewGuid();
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/admin/administrators/{adminId}/reactivate");
        AdminAdministratorsController controller = CreateController(context);
        CancellationTokenSource cts = new();

        ReactivateAdministratorRequest request = new(Revision: 4);
        _sender.RespondWith(Result.Success());

        IActionResult result = await controller.ReactivateAdministrator(adminId, request, cts.Token);

        NoContentResult noContent = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContent.StatusCode);

        Assert.Single(_sender.Requests);
        ReactivateAdministratorCommand command = Assert.IsType<ReactivateAdministratorCommand>(_sender.Requests[0]);
        Assert.Equal(adminId, command.AdministratorId);
        Assert.Equal(4, command.Revision);
        Assert.Equal(cts.Token, _sender.CancellationTokens[0]);
    }

    [Fact]
    public async Task ReactivateAdministrator_Failure_ReturnsProblemDetails()
    {
        Guid adminId = Guid.NewGuid();
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/admin/administrators/{adminId}/reactivate");
        AdminAdministratorsController controller = CreateController(context);

        ReactivateAdministratorRequest request = new(Revision: 1);
        _sender.RespondWith(Result.Failure(Error.NotFound("Admin.NotFound", "Administrator not found.")));

        IActionResult result = await controller.ReactivateAdministrator(adminId, request, CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
    }
}
