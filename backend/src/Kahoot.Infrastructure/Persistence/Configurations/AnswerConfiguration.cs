using Kahoot.Domain.Games;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kahoot.Infrastructure.Persistence.Configurations;

public sealed class AnswerConfiguration : IEntityTypeConfiguration<Answer>
{
    public void Configure(EntityTypeBuilder<Answer> builder)
    {
        builder.HasKey(answer => answer.Id);
        builder.Property(answer => answer.Id).ValueGeneratedNever();

        builder.Property(answer => answer.PointsAwarded).HasDefaultValue(0);

        builder.HasIndex(answer => new { answer.GameSessionId, answer.QuestionId, answer.ParticipantId })
            .IsUnique()
            .HasDatabaseName("uq_answer_participant_question");
        builder.HasIndex(answer => new { answer.GameSessionId, answer.QuestionId })
            .HasDatabaseName("ix_answer_game_question");
        builder.HasIndex(answer => answer.ParticipantId).HasDatabaseName("ix_answer_participant_id");
        builder.HasIndex(answer => answer.SelectedChoiceId).HasDatabaseName("ix_answer_selected_choice_id");

        builder.HasOne(answer => answer.Question)
            .WithMany()
            .HasForeignKey(answer => answer.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(answer => answer.SelectedChoice)
            .WithMany()
            .HasForeignKey(answer => answer.SelectedChoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_answer_points_awarded", "points_awarded >= 0");
            table.HasCheckConstraint("ck_answer_response_time_ms", "response_time_ms >= 0");
        });
    }
}
