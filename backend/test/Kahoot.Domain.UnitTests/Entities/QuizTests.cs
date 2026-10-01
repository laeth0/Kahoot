using Kahoot.Domain.Entities;
using Xunit;

namespace Kahoot.Domain.UnitTests.Entities;

public sealed class QuizTests
{
    [Fact]
    public void NewQuiz_StartsWithInitialRevision()
    {
        Quiz quiz = new() { Title = "Test quiz" };

        Assert.Equal(1L, quiz.Revision);
    }
}
