using Kahoot.Domain.Entities;

namespace Kahoot.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateAccessToken(User user);
}
