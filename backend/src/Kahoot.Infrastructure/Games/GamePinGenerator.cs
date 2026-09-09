using System.Security.Cryptography;
using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Domain.Games;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Infrastructure.Games;

public sealed class GamePinGenerator(IApplicationDbContext dbContext) : IGamePinGenerator, IScopedService
{
    private const int MaxAttempts = 12;
    private const int PinUpperBoundExclusive = 1_000_000;

    public async Task<string> GenerateUniquePinAsync(CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            string pin = RandomNumberGenerator.GetInt32(PinUpperBoundExclusive).ToString("D6");

            bool taken = await dbContext.GameSessions
                .AnyAsync(session => session.Pin == pin && session.Status != GameStatus.Finished, cancellationToken);

            if (!taken)
            {
                return pin;
            }
        }

        throw new InvalidOperationException("Unable to allocate a unique game PIN after multiple attempts.");
    }
}
