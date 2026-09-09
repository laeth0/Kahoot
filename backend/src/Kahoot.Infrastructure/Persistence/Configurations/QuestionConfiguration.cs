using Kahoot.Domain.Quizzes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kahoot.Infrastructure.Persistence.Configurations;

public sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    private const int MinTimeLimitSeconds = 5;
    private const int MaxTimeLimitSeconds = 300;
    private const int DefaultTimeLimitSeconds = 20;
    private const int DefaultPoints = 1000;

    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.HasKey(question => question.Id);
        builder.Property(question => question.Id).ValueGeneratedNever();

        builder.Property(question => question.Text).HasMaxLength(500).IsRequired();
        builder.Property(question => question.TimeLimitSeconds).HasDefaultValue(DefaultTimeLimitSeconds);
        builder.Property(question => question.Points).HasDefaultValue(DefaultPoints);

        builder.Property(question => question.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(question => question.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(question => new { question.QuizId, question.OrderIndex })
            .IsUnique()
            .HasDatabaseName("uq_question_quiz_order");
        builder.HasIndex(question => question.QuizId).HasDatabaseName("ix_question_quiz_id");

        builder.HasMany(question => question.Choices)
            .WithOne(choice => choice.Question)
            .HasForeignKey(choice => choice.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "ck_question_time_limit_seconds",
                $"time_limit_seconds >= {MinTimeLimitSeconds} AND time_limit_seconds <= {MaxTimeLimitSeconds}");
            table.HasCheckConstraint("ck_question_points", "points >= 0");
        });
    }
}
