namespace Kahoot.Api.UnitTests.Features.Admin;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kahoot.Api.Controllers;
using Kahoot.Api.UnitTests.TestSupport;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Admin.Users.GetUserById;
using Kahoot.Application.Features.Admin.Users.ListUsers;
using Kahoot.Application.Features.Admin.Users.ReactivateUser;
using Kahoot.Application.Features.Admin.Users.SuspendUser;
using Kahoot.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

public sealed class AdminUsersControllerTests
{
    private readonly RecordingSender _sender = new();

    private AdminUsersController CreateController(HttpContext httpContext)
    {
        return new AdminUsersController(_sender)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            ProblemDetailsFactory = new TestProblemDetailsFactory()
        };
    }

    [Fact]
    public async Task ListUsers_WithDefaults_DispatchesQueryAndReturnsOk()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/api/admin/users");
        AdminUsersController controller = CreateController(context);
        CancellationTokenSource cts = new();

        ListUsersResponse expectedResponse = new(new List<UserAdminItemResponse>(), null, false);
        _sender.RespondWith(Result.Success(expectedResponse));

        IActionResult result = await controller.ListUsers(cursor: null, pageSize: 50, username: null, status: null, cancellationToken: cts.Token);

        OkObjectResult okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        Assert.Same(expectedResponse, okResult.Value);

        Assert.Single(_sender.Requests);
        ListUsersQuery query = Assert.IsType<ListUsersQuery>(_sender.Requests[0]);
        Assert.Null(query.Cursor);
        Assert.Equal(50, query.PageSize);
        Assert.Null(query.Username);
        Assert.Null(query.Status);
        Assert.Equal(cts.Token, _sender.CancellationTokens[0]);
    }

    [Fact]
    public async Task ListUsers_WithFilters_DispatchesQueryWithFilters()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/api/admin/users");
        AdminUsersController controller = CreateController(context);

        ListUsersResponse expectedResponse = new(new List<UserAdminItemResponse>(), "next-cur", true);
        _sender.RespondWith(Result.Success(expectedResponse));

        IActionResult result = await controller.ListUsers(cursor: "cur123", pageSize: 25, username: "host", status: UserStatus.Active, cancellationToken: CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        ListUsersQuery query = Assert.IsType<ListUsersQuery>(_sender.Requests[0]);
        Assert.Equal("cur123", query.Cursor);
        Assert.Equal(25, query.PageSize);
        Assert.Equal("host", query.Username);
        Assert.Equal(UserStatus.Active, query.Status);
    }

    [Fact]
    public async Task GetUserById_Success_DispatchesQueryAndReturnsOk()
    {
        Guid accountId = Guid.NewGuid();
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/admin/users/{accountId}");
        AdminUsersController controller = CreateController(context);
        CancellationTokenSource cts = new();

        UserAdminResponse expectedResponse = new(
            accountId,
            "samplehost",
            UserRole.Host,
            UserStatus.Active,
            DateTimeOffset.UtcNow,
            null,
            1,
            false);

        _sender.RespondWith(Result.Success(expectedResponse));

        IActionResult result = await controller.GetUserById(accountId, cts.Token);

        OkObjectResult okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        Assert.Same(expectedResponse, okResult.Value);

        Assert.Single(_sender.Requests);
        GetUserByIdQuery query = Assert.IsType<GetUserByIdQuery>(_sender.Requests[0]);
        Assert.Equal(accountId, query.AccountId);
        Assert.Equal(cts.Token, _sender.CancellationTokens[0]);
    }

    [Fact]
    public async Task GetUserById_NotFound_ReturnsProblemDetails()
    {
        Guid accountId = Guid.NewGuid();
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/admin/users/{accountId}");
        AdminUsersController controller = CreateController(context);

        _sender.RespondWith(Result.Failure<UserAdminResponse>(Error.NotFound("User.NotFound", "User not found.")));

        IActionResult result = await controller.GetUserById(accountId, CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
    }

    [Fact]
    public async Task SuspendUser_WithTerminationPending_ReturnsAccepted202()
    {
        Guid accountId = Guid.NewGuid();
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/admin/users/{accountId}/suspend");
        AdminUsersController controller = CreateController(context);
        CancellationTokenSource cts = new();

        SuspendUserRequest request = new(Revision: 4);
        SuspendUserResult serviceResult = new(TerminationPending: true);
        _sender.RespondWith(Result.Success(serviceResult));

        IActionResult result = await controller.SuspendUser(accountId, request, cts.Token);

        AcceptedResult accepted = Assert.IsType<AcceptedResult>(result);
        Assert.Equal(StatusCodes.Status202Accepted, accepted.StatusCode);
        SuspendUserResponse response = Assert.IsType<SuspendUserResponse>(accepted.Value);
        Assert.True(response.TerminationPending);

        Assert.Single(_sender.Requests);
        SuspendUserCommand command = Assert.IsType<SuspendUserCommand>(_sender.Requests[0]);
        Assert.Equal(accountId, command.AccountId);
        Assert.Equal(4, command.Revision);
        Assert.Equal(cts.Token, _sender.CancellationTokens[0]);
    }

    [Fact]
    public async Task SuspendUser_WithoutTerminationPending_ReturnsNoContent204()
    {
        Guid accountId = Guid.NewGuid();
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/admin/users/{accountId}/suspend");
        AdminUsersController controller = CreateController(context);
        CancellationTokenSource cts = new();

        SuspendUserRequest request = new(Revision: 2);
        SuspendUserResult serviceResult = new(TerminationPending: false);
        _sender.RespondWith(Result.Success(serviceResult));

        IActionResult result = await controller.SuspendUser(accountId, request, cts.Token);

        NoContentResult noContent = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContent.StatusCode);

        Assert.Single(_sender.Requests);
        SuspendUserCommand command = Assert.IsType<SuspendUserCommand>(_sender.Requests[0]);
        Assert.Equal(accountId, command.AccountId);
        Assert.Equal(2, command.Revision);
    }

    [Fact]
    public async Task SuspendUser_Failure_ReturnsProblemDetails()
    {
        Guid accountId = Guid.NewGuid();
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/admin/users/{accountId}/suspend");
        AdminUsersController controller = CreateController(context);

        SuspendUserRequest request = new(Revision: 1);
        _sender.RespondWith(Result.Failure<SuspendUserResult>(Error.Conflict("User.ConcurrencyConflict", "Revision mismatch.")));

        IActionResult result = await controller.SuspendUser(accountId, request, CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
    }

    [Fact]
    public async Task ReactivateUser_Success_DispatchesCommandAndReturnsNoContent()
    {
        Guid accountId = Guid.NewGuid();
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/admin/users/{accountId}/reactivate");
        AdminUsersController controller = CreateController(context);
        CancellationTokenSource cts = new();

        ReactivateUserRequest request = new(Revision: 5);
        _sender.RespondWith(Result.Success());

        IActionResult result = await controller.ReactivateUser(accountId, request, cts.Token);

        NoContentResult noContent = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContent.StatusCode);

        Assert.Single(_sender.Requests);
        ReactivateUserCommand command = Assert.IsType<ReactivateUserCommand>(_sender.Requests[0]);
        Assert.Equal(accountId, command.AccountId);
        Assert.Equal(5, command.Revision);
        Assert.Equal(cts.Token, _sender.CancellationTokens[0]);
    }

    [Fact]
    public async Task ReactivateUser_Failure_ReturnsProblemDetails()
    {
        Guid accountId = Guid.NewGuid();
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/admin/users/{accountId}/reactivate");
        AdminUsersController controller = CreateController(context);

        ReactivateUserRequest request = new(Revision: 1);
        _sender.RespondWith(Result.Failure(Error.Conflict("User.AlreadyActive", "User is already active.")));

        IActionResult result = await controller.ReactivateUser(accountId, request, CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
    }
}
