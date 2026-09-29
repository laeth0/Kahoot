using Kahoot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kahoot.Infrastructure.Persistence.Configurations;

public sealed class GameQuestionSnapshotConfiguration : IEntityTypeConfiguration<GameQuestionSnapshot>
{
    public void Configure(EntityTypeBuilder<GameQuestionSnapshot> builder)
    {
        builder.ToTable("game_question_snapshots");

        builder.HasKey(question => question.Id);

        builder.Property(question => question.HostAccountId)
            .IsRequired();

        builder.Property(question => question.GameId)
            .IsRequired();

        builder.Property(question => question.OrderIndex)
            .IsRequired();

        builder.Property(question => question.Text)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(question => question.ImageId);

        builder.Property(question => question.ImageUrl)
            .HasMaxLength(512);

        builder.Property(question => question.DurationSeconds)
            .IsRequired();

        builder.Property(question => question.BasePoints)
            .IsRequired();

        builder.Property(question => question.StartedAt);

        builder.Property(question => question.EndsAt);

        builder.Property(question => question.InitialEligibleParticipantCount)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(question => question.EffectiveEligibleParticipantCount)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(question => question.AcceptedAnswerCount)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(question => question.ResultsMaterializedAt);

        // Composite Multi-Tenant Key - Supports composite foreign key references from child choice snapshots and answer submissions
        builder.HasIndex(question => new { question.Id, question.HostAccountId })
            .IsUnique()
            .HasDatabaseName("ux_game_questions_id_host_account");

        // Composite Multi-Tenant Game Key - Enforces strict three-way tenant partition (Question, Host, Game)
        builder.HasIndex(question => new { question.Id, question.HostAccountId, question.GameId })
            .IsUnique()
            .HasDatabaseName("ux_game_questions_id_host_account_game");

        // Unique Execution Sequence Index - Enforces contiguous zero-based question ordering per game session
        builder.HasIndex(question => new { question.GameId, question.OrderIndex })
            .IsUnique()
            .HasDatabaseName("ux_game_questions_game_order");

        // Composite Index For Question Run Seek - Optimizes fetching the current active question snapshot
        builder.HasIndex(question => new { question.HostAccountId, question.GameId, question.OrderIndex })
            .HasDatabaseName("ix_game_questions_host_account_game_order");

        // Composite Multi-Tenant Foreign Key (TENANT-001) - Links question snapshot strictly to the parent game session
        builder.HasOne<Game>()
            .WithMany()
            .HasForeignKey(question => new { question.GameId, question.HostAccountId })
            .HasPrincipalKey(game => new { game.Id, game.HostAccountId })
            .OnDelete(DeleteBehavior.Restrict);

        // Historical Snapshot Retention Foreign Key (IMG-LIFE-001) - Preserves image asset from cleanup while referenced by snapshots
        builder.HasOne<QuestionImage>()
            .WithMany()
            .HasForeignKey(question => new { question.ImageId, question.HostAccountId })
            .HasPrincipalKey(image => new { image.Id, image.HostAccountId })
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
