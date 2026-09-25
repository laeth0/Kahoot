using Kahoot.Application.Common;
using Kahoot.Domain.Entities;

namespace Kahoot.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    AccessTokenResult GenerateAccessToken(User user);
}
