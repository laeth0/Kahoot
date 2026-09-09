namespace Kahoot.Application.Common.Abstractions;

public interface ISecureTokenGenerator
{
    string GenerateToken();
}
