using Kahoot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kahoot.Infrastructure.Persistence.Configurations;

public sealed class ParticipantSessionTokenConfiguration : IEntityTypeConfiguration<ParticipantSessionToken>
{
    public void Configure(EntityTypeBuilder<ParticipantSessionToken> builder)
    {
        builder.ToTable("participant_session_tokens");

        builder.HasKey(token => token.Id);

        builder.Property(token => token.HostAccountId)
            .IsRequired();

        builder.Property(token => token.GameId)
            .IsRequired();

        builder.Property(token => token.ParticipantId)
            .IsRequired();

        builder.Property(token => token.TokenHash)
            .HasColumnType("bytea")
            .IsRequired();

        builder.Property(token => token.CreatedAt)
            .IsRequired();

        builder.Property(token => token.RevokedAt);

        builder.Property(token => token.ExpiresAt);

        builder.HasIndex(token => token.ParticipantId)
            .IsUnique()
            .HasDatabaseName("ux_participant_session_tokens_participant");

        builder.HasIndex(token => token.TokenHash)
            .IsUnique()
            .HasDatabaseName("ux_participant_session_tokens_token_hash");

        builder.HasIndex(token => new { token.HostAccountId, token.GameId, token.ParticipantId })
            .IsUnique()
            .HasDatabaseName("ux_participant_tokens_host_account_game_participant");

        builder.HasIndex(token => token.ExpiresAt)
            .HasDatabaseName("ix_participant_session_tokens_expires_at");

        builder.HasOne<Participant>()
            .WithMany()
            .HasForeignKey(token => new { token.ParticipantId, token.HostAccountId, token.GameId })
            .HasPrincipalKey(participant => new { participant.Id, participant.HostAccountId, participant.GameId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
