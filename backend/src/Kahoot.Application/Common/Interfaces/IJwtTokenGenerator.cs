using Kahoot.Application.Common;
using Kahoot.Domain.Entities;

namespace Kahoot.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    // Stateless Access Token Issuance (AUTH-JWT-001) - Generates short-lived signed JWT embedding User.TokenSecurityVersion
    AccessTokenResult GenerateAccessToken(User user);
}
