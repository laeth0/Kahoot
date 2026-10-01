namespace Kahoot.Api.UnitTests.Features.Shared;

using System;
using System.Reflection;
using Kahoot.Api.Controllers;
using Kahoot.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

public sealed class ControllerSecurityMetadataDeclarationTests
{
    [Theory]
    [InlineData(typeof(AuthController))]
    [InlineData(typeof(QuizzesController))]
    [InlineData(typeof(GamesController))]
    [InlineData(typeof(ImagesController))]
    [InlineData(typeof(AdminUsersController))]
    [InlineData(typeof(AdminAdministratorsController))]
    public void Controllers_DeclareApiControllerAttribute(Type controllerType)
    {
        ApiControllerAttribute? attribute = controllerType.GetCustomAttribute<ApiControllerAttribute>();
        Assert.NotNull(attribute);
    }

    [Theory]
    [InlineData(typeof(QuizzesController))]
    [InlineData(typeof(GamesController))]
    public void HostControllers_DeclareHostRoleRequirement(Type controllerType)
    {
        AuthorizeAttribute? attribute = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal(nameof(UserRole.Host), attribute.Roles);
    }

    [Theory]
    [InlineData(typeof(AdminUsersController))]
    [InlineData(typeof(AdminAdministratorsController))]
    public void AdminControllers_DeclareSystemAdminRoleRequirement(Type controllerType)
    {
        AuthorizeAttribute? attribute = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal(nameof(UserRole.SystemAdmin), attribute.Roles);
    }

    [Fact]
    public void ImagesController_UploadAction_DeclaresHostRoleRequirement()
    {
        MethodInfo? method = typeof(ImagesController).GetMethod(nameof(ImagesController.UploadImage));
        Assert.NotNull(method);
        AuthorizeAttribute? attribute = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal(nameof(UserRole.Host), attribute.Roles);
    }

    [Theory]
    [InlineData(nameof(ImagesController.GetUploadsRoot))]
    [InlineData(nameof(ImagesController.GetImage))]
    public void ImagesController_PublicReadActions_DeclareAllowAnonymous(string methodName)
    {
        MethodInfo? method = typeof(ImagesController).GetMethod(methodName);
        Assert.NotNull(method);
        AllowAnonymousAttribute? attribute = method.GetCustomAttribute<AllowAnonymousAttribute>();
        Assert.NotNull(attribute);
    }

    [Theory]
    [InlineData(nameof(AuthController.Register))]
    [InlineData(nameof(AuthController.Login))]
    [InlineData(nameof(AuthController.Refresh))]
    [InlineData(nameof(AuthController.Logout))]
    public void AuthController_UnauthenticatedActions_DeclareAllowAnonymous(string methodName)
    {
        MethodInfo? method = typeof(AuthController).GetMethod(methodName);
        Assert.NotNull(method);
        AllowAnonymousAttribute? attribute = method.GetCustomAttribute<AllowAnonymousAttribute>();
        Assert.NotNull(attribute);
    }

    [Theory]
    [InlineData(nameof(AuthController.LogoutAll))]
    [InlineData(nameof(AuthController.ChangePassword))]
    public void AuthController_AuthenticatedActions_DeclareAuthorize(string methodName)
    {
        MethodInfo? method = typeof(AuthController).GetMethod(methodName);
        Assert.NotNull(method);
        AuthorizeAttribute? attribute = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attribute);
    }

    [Theory]
    [InlineData(nameof(GamesController.JoinGame))]
    [InlineData(nameof(GamesController.GetJoinInfo))]
    [InlineData(nameof(GamesController.SubmitAnswer))]
    public void GamesController_AnonymousPlayerActions_DeclareAllowAnonymous(string methodName)
    {
        MethodInfo? method = typeof(GamesController).GetMethod(methodName);
        Assert.NotNull(method);
        AllowAnonymousAttribute? attribute = method.GetCustomAttribute<AllowAnonymousAttribute>();
        Assert.NotNull(attribute);
    }


}
