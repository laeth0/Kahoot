namespace Kahoot.Api.UnitTests.TestSupport;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

public sealed class TestRequestCookiesFeature : IRequestCookiesFeature
{
    public TestRequestCookiesFeature(IRequestCookieCollection cookies)
    {
        Cookies = cookies;
    }

    public IRequestCookieCollection Cookies { get; set; }
}
