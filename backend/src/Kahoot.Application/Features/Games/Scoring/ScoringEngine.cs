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
        TimeSpan elapsed,
        bool isCorrect)
    {
        if (!isCorrect || basePoints <= 0)
        {
            return 0;
        }

        if (durationSeconds <= 0)
        {
            return basePoints;
        }

        long durationTicks = TimeSpan.FromSeconds(durationSeconds).Ticks;
        long elapsedTicks = Math.Clamp(elapsed.Ticks, 0L, durationTicks);
        Int128 denominator = 2 * (Int128)durationTicks;
        Int128 numerator = (Int128)basePoints * (denominator - elapsedTicks);

        // The half-denominator adjustment rounds positive half points away from zero exactly.
        return checked((int)((numerator + durationTicks) / denominator));
    }
}
