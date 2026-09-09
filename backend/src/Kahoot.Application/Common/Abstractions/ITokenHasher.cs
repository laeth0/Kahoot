namespace Kahoot.Application.Common.Abstractions;

public interface ITokenHasher
{
    string Hash(string token);
}
