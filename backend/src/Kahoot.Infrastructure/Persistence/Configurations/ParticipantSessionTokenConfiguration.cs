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

        // Participant Token Lookup Index - Optimizes querying tokens issued for a specific participant
        builder.HasIndex(token => token.ParticipantId)
            .HasDatabaseName("ix_participant_session_tokens_participant");

        // Unique Cryptographic Digest Index (JOIN-AUTH-001) - Guarantees uniqueness for SHA-256 bearer token digests
        builder.HasIndex(token => token.TokenHash)
            .IsUnique()
            .HasDatabaseName("ux_participant_session_tokens_token_hash");

        // Composite Multi-Tenant Key - Supports token queries partitioned by host and game
        builder.HasIndex(token => new { token.HostAccountId, token.GameId, token.ParticipantId })
            .HasDatabaseName("ix_participant_tokens_host_account_game_participant");

        // Expiration Index - Optimizes periodic cleanup of expired player session tokens
        builder.HasIndex(token => token.ExpiresAt)
            .HasDatabaseName("ix_participant_session_tokens_expires_at");

        // Composite Multi-Tenant Foreign Key (TENANT-001) - Links token strictly to parent participant within the game session
        builder.HasOne<Participant>()
            .WithMany()
            .HasForeignKey(token => new { token.ParticipantId, token.HostAccountId, token.GameId })
            .HasPrincipalKey(participant => new { participant.Id, participant.HostAccountId, participant.GameId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
