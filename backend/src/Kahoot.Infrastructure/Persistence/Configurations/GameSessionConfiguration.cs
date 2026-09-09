using Kahoot.Domain.Games;
using Kahoot.Domain.Quizzes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kahoot.Infrastructure.Persistence.Configurations;

public sealed class GameSessionConfiguration : IEntityTypeConfiguration<GameSession>
{
    public void Configure(EntityTypeBuilder<GameSession> builder)
    {
        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).ValueGeneratedNever();

        builder.Property(session => session.Pin).HasMaxLength(8).IsRequired();

        builder.Property(session => session.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValueSql("'Created'")
            .IsRequired();

        builder.Property(session => session.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(session => session.UpdatedAt).HasDefaultValueSql("now()");

        builder.Property<uint>("Version")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasIndex(session => session.Pin)
            .IsUnique()
            .HasFilter("status <> 'Finished'")
            .HasDatabaseName("uq_game_session_active_pin");
        builder.HasIndex(session => session.HostId).HasDatabaseName("ix_game_session_host_id");
        builder.HasIndex(session => session.QuizId).HasDatabaseName("ix_game_session_quiz_id");
        builder.HasIndex(session => session.Status).HasDatabaseName("ix_game_session_status");
        builder.HasIndex(session => session.CurrentQuestionId).HasDatabaseName("ix_game_session_current_question_id");

        builder.HasOne(session => session.Quiz)
            .WithMany()
            .HasForeignKey(session => session.QuizId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(session => session.Host)
            .WithMany()
            .HasForeignKey(session => session.HostId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Question>()
            .WithMany()
            .HasForeignKey(session => session.CurrentQuestionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(session => session.Participants)
            .WithOne(participant => participant.GameSession)
            .HasForeignKey(participant => participant.GameSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(session => session.Answers)
            .WithOne(answer => answer.GameSession)
            .HasForeignKey(answer => answer.GameSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(table => table.HasCheckConstraint(
            "ck_game_session_question_window",
            "current_question_ends_at IS NULL OR current_question_started_at IS NULL "
            + "OR current_question_ends_at >= current_question_started_at"));
    }
}
