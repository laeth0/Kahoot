using Kahoot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kahoot.Infrastructure.Persistence.Configurations;

public sealed class ParticipantConfiguration : IEntityTypeConfiguration<Participant>
{
    public void Configure(EntityTypeBuilder<Participant> builder)
    {
        builder.ToTable("participants");

        builder.HasKey(participant => participant.Id);

        builder.Property(participant => participant.HostAccountId)
            .IsRequired();

        builder.Property(participant => participant.GameId)
            .IsRequired();

        builder.Property(participant => participant.DisplayNickname)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(participant => participant.NormalizedNickname)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(participant => participant.SeatNumber)
            .IsRequired();

        builder.Property(participant => participant.JoinOperationIdHash)
            .HasColumnType("bytea")
            .IsRequired();

        builder.Property(participant => participant.JoinRecoveryExpiresAt)
            .IsRequired();

        builder.Property(participant => participant.IsRemoved)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(participant => participant.RemovedAt);

        builder.Property(participant => participant.TotalScore)
            .HasDefaultValue(0L)
            .IsRequired();

        builder.Property(participant => participant.Rank);

        builder.Property(participant => participant.ConnectionGeneration)
            .HasDefaultValue(0L)
            .IsRequired();

        builder.Property(participant => participant.CreatedAt)
            .IsRequired();

        // Composite Multi-Tenant Key - Supports three-way partition (Participant, Host, Game) for answer submissions
        builder.HasIndex(participant => new { participant.Id, participant.HostAccountId, participant.GameId })
            .IsUnique()
            .HasDatabaseName("ux_participants_id_host_account_game");

        // Permanent Nickname Tombstone (JOIN-NICK-002) - Enforces unique nickname per game session; retained after ejection to prevent reuse
        builder.HasIndex(participant => new { participant.GameId, participant.NormalizedNickname })
            .IsUnique()
            .HasDatabaseName("ux_participants_game_nickname");

        // Join Idempotency Key (JOIN-IDEMP-001) - Guarantees at most one participant record per client-provided JoinOperationId per game
        builder.HasIndex(participant => new { participant.GameId, participant.JoinOperationIdHash })
            .IsUnique()
            .HasDatabaseName("ux_participants_game_join_operation");

        // Global Join Operation Index - Supports fast lookup for recovery queries
        builder.HasIndex(participant => participant.JoinOperationIdHash)
            .IsUnique()
            .HasDatabaseName("ux_participants_join_operation");

        // Unique Seat Allocation Index (JOIN-CAP-001) - Enforces contiguous unique seat numbers (1 to 500) per game
        builder.HasIndex(participant => new { participant.GameId, participant.SeatNumber })
            .IsUnique()
            .HasDatabaseName("ux_participants_game_seat");

        // Active Participant Filter Index - Optimizes lobby listing and dynamic auto-close participant counts
        builder.HasIndex(participant => new { participant.HostAccountId, participant.GameId, participant.IsRemoved })
            .HasDatabaseName("ix_participants_host_account_game_removed");

        // Composite Multi-Tenant Foreign Key (TENANT-001) - Scopes participant strictly to the parent live game session
        builder.HasOne<Game>()
            .WithMany()
            .HasForeignKey(participant => new { participant.GameId, participant.HostAccountId })
            .HasPrincipalKey(game => new { game.Id, game.HostAccountId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
