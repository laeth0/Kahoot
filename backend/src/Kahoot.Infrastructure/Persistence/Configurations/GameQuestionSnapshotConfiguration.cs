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

        builder.Property(question => question.MediaItemId);

        builder.Property(question => question.MediaUrl)
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

        builder.HasIndex(question => new { question.Id, question.HostAccountId })
            .IsUnique()
            .HasDatabaseName("ux_game_questions_id_host_account");

        builder.HasIndex(question => new { question.Id, question.HostAccountId, question.GameId })
            .IsUnique()
            .HasDatabaseName("ux_game_questions_id_host_account_game");

        builder.HasIndex(question => new { question.GameId, question.OrderIndex })
            .IsUnique()
            .HasDatabaseName("ux_game_questions_game_order");

        builder.HasIndex(question => new { question.HostAccountId, question.GameId, question.OrderIndex })
            .HasDatabaseName("ix_game_questions_host_account_game_order");

        builder.HasOne<Game>()
            .WithMany()
            .HasForeignKey(question => new { question.GameId, question.HostAccountId })
            .HasPrincipalKey(game => new { game.Id, game.HostAccountId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<MediaItem>()
            .WithMany()
            .HasForeignKey(question => new { question.MediaItemId, question.HostAccountId })
            .HasPrincipalKey(media => new { media.Id, media.HostAccountId })
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
