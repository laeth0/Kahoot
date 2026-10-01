namespace Kahoot.Api.UnitTests.Services;

using System;
using System.Security.Claims;
using Kahoot.Api.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

public sealed class CurrentUserTests
{
    [Fact]
    public void CurrentUser_NoContextReturnsAnonymousValues()
    {
        IHttpContextAccessor accessor = new HttpContextAccessor { HttpContext = null };
        CurrentUser currentUser = new(accessor);

        Assert.Null(currentUser.UserId);
        Assert.Null(currentUser.Role);
        Assert.False(currentUser.IsAuthenticated);
    }

    [Fact]
    public void UserId_ReadsNameIdentifierBeforeSub()
    {
        Guid nameIdentifierGuid = Guid.Parse("01918a3d-4e2b-7c15-8fa9-33b66479b001");
        Guid subGuid = Guid.Parse("01918a3d-4e2b-7c15-8fa9-33b66479b002");

        Claim[] claimsWithBoth =
        [
            new Claim(ClaimTypes.NameIdentifier, nameIdentifierGuid.ToString()),
            new Claim("sub", subGuid.ToString())
        ];

        CurrentUser userWithBoth = CreateCurrentUser(claimsWithBoth);
        Assert.Equal(nameIdentifierGuid, userWithBoth.UserId);

        Claim[] claimsWithSubOnly =
        [
            new Claim("sub", subGuid.ToString())
        ];

        CurrentUser userWithSubOnly = CreateCurrentUser(claimsWithSubOnly);
        Assert.Equal(subGuid, userWithSubOnly.UserId);
    }

    [Fact]
    public void UserId_InvalidPreferredClaimDoesNotFallBackToValidSub()
    {
        Guid subGuid = Guid.Parse("01918a3d-4e2b-7c15-8fa9-33b66479b003");

        Claim[] claims =
        [
            new Claim(ClaimTypes.NameIdentifier, "invalid-not-a-guid"),
            new Claim("sub", subGuid.ToString())
        ];

        CurrentUser currentUser = CreateCurrentUser(claims);
        Assert.Null(currentUser.UserId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid-at-all")]
    [InlineData("12345")]
    public void UserId_ReturnsNullForMissingOrMalformedValue(string? rawClaimValue)
    {
        Claim[] claims = rawClaimValue is null
            ? Array.Empty<Claim>()
            : [new Claim(ClaimTypes.NameIdentifier, rawClaimValue)];

        CurrentUser currentUser = CreateCurrentUser(claims);
        Assert.Null(currentUser.UserId);
    }

    [Fact]
    public void UserId_ParsesGuidEmptySuccessfully()
    {
        Claim[] claims = [new Claim(ClaimTypes.NameIdentifier, Guid.Empty.ToString())];
        CurrentUser currentUser = CreateCurrentUser(claims);

        Assert.Equal(Guid.Empty, currentUser.UserId);
    }

    [Fact]
    public void Role_ReadsMappedRoleBeforeRawRole()
    {
        Claim[] claimsWithBoth =
        [
            new Claim(ClaimTypes.Role, "MappedHostRole"),
            new Claim("role", "RawRoleValue")
        ];

        CurrentUser userWithBoth = CreateCurrentUser(claimsWithBoth);
        Assert.Equal("MappedHostRole", userWithBoth.Role);

        Claim[] claimsWithRawOnly =
        [
            new Claim("role", "RawRoleValue")
        ];

        CurrentUser userWithRawOnly = CreateCurrentUser(claimsWithRawOnly);
        Assert.Equal("RawRoleValue", userWithRawOnly.Role);

        CurrentUser anonymousUser = CreateCurrentUser(Array.Empty<Claim>());
        Assert.Null(anonymousUser.Role);
    }

    [Fact]
    public void IsAuthenticated_ReflectsPrincipalIdentity()
    {
        ClaimsIdentity authenticatedIdentity = new([new Claim(ClaimTypes.Name, "Alice")], "CustomAuthType");
        ClaimsPrincipal authenticatedPrincipal = new(authenticatedIdentity);
        IHttpContextAccessor authAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = authenticatedPrincipal }
        };
        CurrentUser authUser = new(authAccessor);
        Assert.True(authUser.IsAuthenticated);

        ClaimsIdentity unauthenticatedIdentity = new([new Claim(ClaimTypes.Name, "Bob")], authenticationType: null);
        ClaimsPrincipal unauthenticatedPrincipal = new(unauthenticatedIdentity);
        IHttpContextAccessor unauthAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = unauthenticatedPrincipal }
        };
        CurrentUser unauthUser = new(unauthAccessor);
        Assert.False(unauthUser.IsAuthenticated);
    }

    private static CurrentUser CreateCurrentUser(Claim[] claims)
    {
        ClaimsIdentity identity = new(claims, "Bearer");
        ClaimsPrincipal principal = new(identity);
        DefaultHttpContext context = new() { User = principal };
        IHttpContextAccessor accessor = new HttpContextAccessor { HttpContext = context };

        return new CurrentUser(accessor);
    }
}
