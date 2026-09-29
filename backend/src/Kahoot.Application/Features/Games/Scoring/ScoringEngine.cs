namespace Kahoot.Application.Features.Games.Scoring;

public static class ScoringEngine
{
    // Exact-Set Correctness Evaluation (SCORE-EXACT-001) - Verifies participant choices match all correct options with zero incorrect choices.
    public static bool EvaluateCorrectness(
        IEnumerable<Guid> submittedChoiceIds,
        IEnumerable<Guid> correctChoiceIds)
    {
        HashSet<Guid> actualSet = [.. submittedChoiceIds];
        HashSet<Guid> correctSet = [.. correctChoiceIds];

        return actualSet.SetEquals(correctSet);
    }

    // Speed-Scaled Scoring Formula (SCORE-FORM-001) - Calculates points with time clamping and midpoint rounding away from zero.
    public static int CalculatePoints(
        int basePoints,
        int durationSeconds,
        double elapsedSeconds,
        bool isCorrect)
    {
        // Zero-Score Short-Circuit - Incorrect answers or questions configured with zero base points award exactly 0 points.
        if (!isCorrect || basePoints <= 0)
        {
            return 0;
        }

        if (durationSeconds <= 0)
        {
            return basePoints;
        }

        // Bounded Response Time Clamping (SCORE-BOUND-001) - Clamps elapsed time to [0, durationSeconds] to prevent clock anomaly point inflation.
        double clampedTime = Math.Clamp(elapsedSeconds, 0.0, (double)durationSeconds);
        // Speed Decay Multiplier - Scales score linearly from 100% at t=0 down to 50% at deadline t=D.
        double speedDecayFraction = 1.0 - (0.5 * (clampedTime / durationSeconds));
        double rawPoints = basePoints * speedDecayFraction;

        // Midpoint Rounding Away From Zero (SCORE-ROUND-001) - Ensures odd base points round predictably up at 50% midpoint.
        return (int)Math.Round(rawPoints, MidpointRounding.AwayFromZero);
    }
}
