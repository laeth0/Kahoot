using Kahoot.Application.Features.Games.Scoring;
using Xunit;

namespace Kahoot.Application.UnitTests.Features.Games.Scoring;

public sealed class ScoringEngineTests
{
    [Fact]
    public void EvaluateCorrectness_WhenChoicesMatchExactly_ReturnsTrue()
    {
        Guid choice1 = Guid.NewGuid();
        Guid choice2 = Guid.NewGuid();

        bool result = ScoringEngine.EvaluateCorrectness(
            new[] { choice1, choice2 },
            new[] { choice1, choice2 });

        Assert.True(result);
    }

    [Fact]
    public void EvaluateCorrectness_IsOrderIndependent()
    {
        Guid choice1 = Guid.NewGuid();
        Guid choice2 = Guid.NewGuid();
        Guid choice3 = Guid.NewGuid();

        bool result = ScoringEngine.EvaluateCorrectness(
            new[] { choice3, choice1, choice2 },
            new[] { choice1, choice2, choice3 });

        Assert.True(result);
    }

    [Fact]
    public void EvaluateCorrectness_IgnoresDuplicateSubmittedSelections()
    {
        Guid choice1 = Guid.NewGuid();
        Guid choice2 = Guid.NewGuid();

        bool result = ScoringEngine.EvaluateCorrectness(
            new[] { choice1, choice1, choice2, choice2 },
            new[] { choice1, choice2 });

        Assert.True(result);
    }

    [Fact]
    public void EvaluateCorrectness_WhenPartialChoicesSubmitted_ReturnsFalse()
    {
        Guid choice1 = Guid.NewGuid();
        Guid choice2 = Guid.NewGuid();

        bool result = ScoringEngine.EvaluateCorrectness(
            new[] { choice1 },
            new[] { choice1, choice2 });

        Assert.False(result);
    }

    [Fact]
    public void EvaluateCorrectness_WhenExtraChoicesSubmitted_ReturnsFalse()
    {
        Guid choice1 = Guid.NewGuid();
        Guid choice2 = Guid.NewGuid();
        Guid extraChoice = Guid.NewGuid();

        bool result = ScoringEngine.EvaluateCorrectness(
            new[] { choice1, choice2, extraChoice },
            new[] { choice1, choice2 });

        Assert.False(result);
    }

    [Fact]
    public void EvaluateCorrectness_WhenCompletelyWrongChoicesSubmitted_ReturnsFalse()
    {
        Guid choice1 = Guid.NewGuid();
        Guid choice2 = Guid.NewGuid();

        bool result = ScoringEngine.EvaluateCorrectness(
            new[] { choice1 },
            new[] { choice2 });

        Assert.False(result);
    }

    [Fact]
    public void EvaluateCorrectness_WhenEmptySubmittedAndCorrectIsNonEmpty_ReturnsFalse()
    {
        Guid choice1 = Guid.NewGuid();

        bool result = ScoringEngine.EvaluateCorrectness(
            Array.Empty<Guid>(),
            new[] { choice1 });

        Assert.False(result);
    }

    [Fact]
    public void CalculatePoints_WhenIncorrect_ReturnsZero()
    {
        int points = ScoringEngine.CalculatePoints(
            basePoints: 1000,
            durationSeconds: 30,
            elapsed: TimeSpan.Zero,
            isCorrect: false);

        Assert.Equal(0, points);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void CalculatePoints_WhenBasePointsZeroOrNegative_ReturnsZero(int basePoints)
    {
        int points = ScoringEngine.CalculatePoints(
            basePoints: basePoints,
            durationSeconds: 30,
            elapsed: TimeSpan.Zero,
            isCorrect: true);

        Assert.Equal(0, points);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void CalculatePoints_WhenDurationZeroOrNegative_ReturnsBasePoints(int durationSeconds)
    {
        int points = ScoringEngine.CalculatePoints(
            basePoints: 1000,
            durationSeconds: durationSeconds,
            elapsed: TimeSpan.FromSeconds(10),
            isCorrect: true);

        Assert.Equal(1000, points);
    }

    [Fact]
    public void CalculatePoints_AtStartOfQuestion_ReturnsFullBasePoints()
    {
        int points = ScoringEngine.CalculatePoints(
            basePoints: 1000,
            durationSeconds: 30,
            elapsed: TimeSpan.Zero,
            isCorrect: true);

        Assert.Equal(1000, points);
    }

    [Fact]
    public void CalculatePoints_AtMidpointOfQuestion_ReturnsSeventyFivePercentPoints()
    {
        int points = ScoringEngine.CalculatePoints(
            basePoints: 1000,
            durationSeconds: 30,
            elapsed: TimeSpan.FromSeconds(15),
            isCorrect: true);

        Assert.Equal(750, points);
    }

    [Fact]
    public void CalculatePoints_AtDeadlineOfQuestion_ReturnsFiftyPercentPoints()
    {
        int points = ScoringEngine.CalculatePoints(
            basePoints: 1000,
            durationSeconds: 30,
            elapsed: TimeSpan.FromSeconds(30),
            isCorrect: true);

        Assert.Equal(500, points);
    }

    [Fact]
    public void CalculatePoints_WhenElapsedIsNegative_ClampsToZeroAndReturnsFullPoints()
    {
        int points = ScoringEngine.CalculatePoints(
            basePoints: 1000,
            durationSeconds: 30,
            elapsed: TimeSpan.FromSeconds(-5),
            isCorrect: true);

        Assert.Equal(1000, points);
    }

    [Fact]
    public void CalculatePoints_WhenElapsedExceedsDuration_ClampsToDurationAndReturnsFiftyPercentPoints()
    {
        int points = ScoringEngine.CalculatePoints(
            basePoints: 1000,
            durationSeconds: 30,
            elapsed: TimeSpan.FromSeconds(45),
            isCorrect: true);

        Assert.Equal(500, points);
    }

    [Fact]
    public void CalculatePoints_RoundsMidpointAwayFromZero()
    {
        // basePoints = 1, duration = 2s, elapsed = 2s -> points formula yields 0.5, rounded to 1
        int points = ScoringEngine.CalculatePoints(
            basePoints: 1,
            durationSeconds: 2,
            elapsed: TimeSpan.FromSeconds(2),
            isCorrect: true);

        Assert.Equal(1, points);
    }

    [Fact]
    public void CalculatePoints_CalculatesWithTickPrecision()
    {
        TimeSpan elapsed1 = TimeSpan.FromTicks(10_000_000); // 1 second
        TimeSpan elapsed2 = TimeSpan.FromTicks(10_000_001); // 1 second + 1 tick

        int points1 = ScoringEngine.CalculatePoints(1000, 30, elapsed1, true);
        int points2 = ScoringEngine.CalculatePoints(1000, 30, elapsed2, true);

        Assert.True(points1 >= points2);
    }

    [Fact]
    public void CalculatePoints_WithIntMaxValueBasePoints_DoesNotOverflow()
    {
        int maxBase = int.MaxValue;

        int startPoints = ScoringEngine.CalculatePoints(
            basePoints: maxBase,
            durationSeconds: 300,
            elapsed: TimeSpan.Zero,
            isCorrect: true);

        Assert.Equal(maxBase, startPoints);

        int deadlinePoints = ScoringEngine.CalculatePoints(
            basePoints: maxBase,
            durationSeconds: 300,
            elapsed: TimeSpan.FromSeconds(300),
            isCorrect: true);

        // int.MaxValue * 0.5 = 2,147,483,647 / 2 = 1073741823.5 -> rounded away from zero = 1073741824
        Assert.Equal(1073741824, deadlinePoints);
    }
}
