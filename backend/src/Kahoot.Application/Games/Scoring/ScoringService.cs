using Kahoot.Application.Common.Interfaces;

namespace Kahoot.Application.Games.Scoring;

public sealed class ScoringService : IScoringService, ISingletonService
{
    public int CalculateScore(bool isCorrect, TimeSpan responseTime, int timeLimitSeconds, int basePoints)
    {
        if (!isCorrect || basePoints <= 0)
        {
            return 0;
        }

        double limitSeconds = Math.Max(timeLimitSeconds, 1);
        double elapsedSeconds = Math.Clamp(responseTime.TotalSeconds, 0d, limitSeconds);
        double speedFactor = 1d - (elapsedSeconds / limitSeconds / 2d);

        int rawScore = (int)Math.Round(basePoints * speedFactor, MidpointRounding.AwayFromZero);
        int minimumScore = (int)Math.Ceiling(basePoints / 2d);

        return Math.Clamp(rawScore, minimumScore, basePoints);
    }
}
