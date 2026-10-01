namespace Kahoot.Api.UnitTests.TestSupport;

using Kahoot.Api.Controllers;
using Kahoot.Application.Common.Results;
using Microsoft.AspNetCore.Mvc;

public sealed class TestApiController : ApiController
{
    public IActionResult MapProblem(Error error) => Problem(error);
}
