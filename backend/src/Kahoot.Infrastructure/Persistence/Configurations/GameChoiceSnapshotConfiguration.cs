using Kahoot.Domain.Games;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kahoot.Infrastructure.Persistence.Configurations;

public sealed class GameChoiceSnapshotConfiguration : IEntityTypeConfiguration<GameChoiceSnapshot>
{
    public void Configure(EntityTypeBuilder<GameChoiceSnapshot> builder)
    {
        builder.HasKey(choice => choice.Id);
        builder.Property(choice => choice.Id).ValueGeneratedNever();

        builder.Property(choice => choice.Text).IsRequired().HasMaxLength(300);
        builder.Property(choice => choice.IsCorrect).HasDefaultValue(false);

        builder.HasIndex(choice => new { choice.QuestionSnapshotId, choice.OrderIndex })
            .IsUnique()
            .HasDatabaseName("uq_game_choice_snapshot_order");
        builder.HasIndex(choice => choice.QuestionSnapshotId).HasDatabaseName("ix_game_choice_snapshot_question_id");
    }
}
