using Kahoot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kahoot.Infrastructure.Persistence.Configurations;

public sealed class GameConfiguration : IEntityTypeConfiguration<Game>
{
    public void Configure(EntityTypeBuilder<Game> builder)
    {
        builder.ToTable("games");

        builder.HasKey(game => game.Id);

        builder.Property(game => game.HostAccountId)
            .IsRequired();

        builder.Property(game => game.SourceQuizId)
            .IsRequired();

        builder.Property(game => game.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(game => game.Pin)
            .HasMaxLength(8);

        builder.Property(game => game.Status)
            .IsRequired();

        builder.Property(game => game.StateVersion)
            .HasDefaultValue(1L)
            .IsRequired();

        builder.Property(game => game.PresenceVersion)
            .HasDefaultValue(0L)
            .IsRequired();

        builder.Property(game => game.ReservedParticipantCount)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(game => game.NextSeatNumber)
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(game => game.CurrentQuestionIndex);

        builder.Property(game => game.HostGraceExpiresAt);

        builder.Property(game => game.IsTerminatedBySuspension)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(game => game.CreatedAt)
            .IsRequired();

        builder.Property(game => game.FinishedAt);

        builder.HasIndex(game => new { game.Id, game.HostAccountId })
            .IsUnique()
            .HasDatabaseName("ux_games_id_host_account");

        builder.HasIndex(game => game.Pin)
            .IsUnique()
            .HasDatabaseName("ux_games_active_pin");

        builder.HasIndex(game => new { game.HostAccountId, game.CreatedAt, game.Id })
            .HasDatabaseName("ix_games_host_account_created_id");

        builder.HasIndex(game => new { game.HostAccountId, game.Status })
            .HasDatabaseName("ix_games_host_account_status");

        builder.HasIndex(game => new { game.HostAccountId, game.SourceQuizId })
            .HasDatabaseName("ix_games_host_account_source_quiz");

        builder.HasIndex(game => new { game.Status, game.HostGraceExpiresAt })
            .HasDatabaseName("ix_games_abandonment_sweep");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(game => game.HostAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Quiz>()
            .WithMany()
            .HasForeignKey(game => new { game.SourceQuizId, game.HostAccountId })
            .HasPrincipalKey(quiz => new { quiz.Id, quiz.HostAccountId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
