namespace Kahoot.Application.Games.Scoring;

public interface IScoringService
{
    int CalculateScore(bool isCorrect, TimeSpan responseTime, int timeLimitSeconds, int basePoints);
}
