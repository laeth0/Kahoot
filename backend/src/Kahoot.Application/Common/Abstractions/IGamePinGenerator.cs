namespace Kahoot.Application.Common.Abstractions;

public interface IGamePinGenerator
{
    Task<string> GenerateUniquePinAsync(CancellationToken cancellationToken);
}
