using Kahoot.Domain.Games;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kahoot.Infrastructure.Persistence.Configurations;

public sealed class GameQuestionSnapshotConfiguration : IEntityTypeConfiguration<GameQuestionSnapshot>
{
    private const int MinTimeLimitSeconds = 5;
    private const int MaxTimeLimitSeconds = 300;
    private const int DefaultTimeLimitSeconds = 20;
    private const int DefaultPoints = 1000;

    public void Configure(EntityTypeBuilder<GameQuestionSnapshot> builder)
    {
        builder.HasKey(snapshot => snapshot.Id);
        builder.Property(snapshot => snapshot.Id).ValueGeneratedNever();

        builder.Property(snapshot => snapshot.Text).HasMaxLength(500).IsRequired();
        builder.Property(snapshot => snapshot.ImageUrl).HasMaxLength(2048);
        builder.Property(snapshot => snapshot.TimeLimitSeconds).HasDefaultValue(DefaultTimeLimitSeconds);
        builder.Property(snapshot => snapshot.Points).HasDefaultValue(DefaultPoints);

        builder.HasIndex(snapshot => new { snapshot.GameSessionId, snapshot.OrderIndex })
            .IsUnique()
            .HasDatabaseName("uq_game_question_snapshot_order");
        builder.HasIndex(snapshot => snapshot.GameSessionId).HasDatabaseName("ix_game_question_snapshot_session_id");

        builder.HasMany(snapshot => snapshot.Choices)
            .WithOne(choice => choice.QuestionSnapshot)
            .HasForeignKey(choice => choice.QuestionSnapshotId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "ck_game_question_snapshot_time_limit_seconds",
                $"time_limit_seconds >= {MinTimeLimitSeconds} AND time_limit_seconds <= {MaxTimeLimitSeconds}");
            table.HasCheckConstraint("ck_game_question_snapshot_points", "points >= 0");
        });
    }
}
