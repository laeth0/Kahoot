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

        builder.HasIndex(participant => new { participant.Id, participant.HostAccountId, participant.GameId })
            .IsUnique()
            .HasDatabaseName("ux_participants_id_host_account_game");

        builder.HasIndex(participant => new { participant.GameId, participant.NormalizedNickname })
            .IsUnique()
            .HasDatabaseName("ux_participants_game_nickname");

        builder.HasIndex(participant => new { participant.GameId, participant.JoinOperationIdHash })
            .IsUnique()
            .HasDatabaseName("ux_participants_game_join_operation");

        builder.HasIndex(participant => new { participant.GameId, participant.SeatNumber })
            .IsUnique()
            .HasDatabaseName("ux_participants_game_seat");

        builder.HasIndex(participant => new { participant.HostAccountId, participant.GameId, participant.IsRemoved })
            .HasDatabaseName("ix_participants_host_account_game_removed");

        builder.HasOne<Game>()
            .WithMany()
            .HasForeignKey(participant => new { participant.GameId, participant.HostAccountId })
            .HasPrincipalKey(game => new { game.Id, game.HostAccountId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
