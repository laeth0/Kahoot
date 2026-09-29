using Kahoot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kahoot.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(token => token.Id);

        builder.Property(token => token.UserId)
            .IsRequired();

        builder.Property(token => token.TokenFamilyId)
            .IsRequired();

        builder.Property(token => token.FamilyCreatedAt)
            .IsRequired();

        builder.Property(token => token.TokenHash)
            .HasColumnType("bytea")
            .IsRequired();

        builder.Property(token => token.CreatedAt)
            .IsRequired();

        builder.Property(token => token.ExpiresAt)
            .IsRequired();

        builder.Property(token => token.RotatedAt);

        builder.Property(token => token.RevokedAt);

        // Unique Cryptographic Digest Index (AUTH-SEC-001) - Guarantees uniqueness for SHA-256 refresh token digests
        builder.HasIndex(token => token.TokenHash)
            .IsUnique()
            .HasDatabaseName("ux_refresh_tokens_token_hash");

        // Family Tracking Index (AUTH-ROT-001) - Optimizes querying and mass-revoking tokens belonging to the same rotation chain
        builder.HasIndex(token => new { token.UserId, token.TokenFamilyId })
            .HasDatabaseName("ix_refresh_tokens_user_family");

        // Expiration Index (AUTH-EXP-001) - Optimizes background cleanup sweeps of expired refresh tokens
        builder.HasIndex(token => token.ExpiresAt)
            .HasDatabaseName("ix_refresh_tokens_expires_at");

        // Revocation Index - Optimizes checking or purging explicitly revoked tokens
        builder.HasIndex(token => token.RevokedAt)
            .HasDatabaseName("ix_refresh_tokens_revoked_at");

        // Referential Cascade Delete - Automatically purges refresh tokens when associated user account is permanently deleted
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
