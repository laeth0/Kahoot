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

        // Composite Multi-Tenant Key - Supports composite foreign key references from participants and question snapshots
        builder.HasIndex(game => new { game.Id, game.HostAccountId })
            .IsUnique()
            .HasDatabaseName("ux_games_id_host_account");

        // Unique Active PIN Index (JOIN-PIN-001) - Guarantees unique discovery PIN for active game sessions
        builder.HasIndex(game => game.Pin)
            .IsUnique()
            .HasDatabaseName("ux_games_active_pin");

        // Keyset Pagination Index - Supports host game history listing sorted by creation date
        builder.HasIndex(game => new { game.HostAccountId, game.CreatedAt, game.Id })
            .HasDatabaseName("ix_games_host_account_created_id");

        // Active Session Check Index (QUIZ-ERR-006) - Optimizes checking whether a host has active live games
        builder.HasIndex(game => new { game.HostAccountId, game.Status })
            .HasDatabaseName("ix_games_host_account_status");

        // Source Quiz Link Index (QUIZ-DEL-001) - Optimizes ever-played checks preventing deletion of launched quizzes
        builder.HasIndex(game => new { game.HostAccountId, game.SourceQuizId })
            .HasDatabaseName("ix_games_host_account_source_quiz");

        // Abandonment Worker Sweep Index (HOST-PRES-001) - Optimizes periodic sweep of abandoned sessions by status and lease deadline
        builder.HasIndex(game => new { game.Status, game.HostGraceExpiresAt })
            .HasDatabaseName("ix_games_abandonment_sweep");

        // Referential Integrity Constraint - Restricts deletion of host User while game records exist
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(game => game.HostAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // Composite Multi-Tenant Foreign Key (TENANT-001) - Links game to source quiz strictly within the same host tenant
        builder.HasOne<Quiz>()
            .WithMany()
            .HasForeignKey(game => new { game.SourceQuizId, game.HostAccountId })
            .HasPrincipalKey(quiz => new { quiz.Id, quiz.HostAccountId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
