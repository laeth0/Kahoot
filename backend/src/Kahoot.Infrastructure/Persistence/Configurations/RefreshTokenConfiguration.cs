using Kahoot.Domain.Hosts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kahoot.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).ValueGeneratedNever();

        builder.Property(token => token.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(token => token.FamilyId).IsRequired();
        builder.Property(token => token.CreatedAt).IsRequired();

        builder.HasIndex(token => token.TokenHash).IsUnique().HasDatabaseName("uq_refresh_token_hash");
        builder.HasIndex(token => token.HostId).HasDatabaseName("ix_refresh_token_host_id");
        builder.HasIndex(token => token.FamilyId).HasDatabaseName("ix_refresh_tokens_family_id");
        builder.HasIndex(token => new { token.FamilyId, token.RevokedAt }).HasDatabaseName("ix_refresh_tokens_family_revoked");
        builder.HasIndex(token => token.ExpiresAt).HasDatabaseName("ix_refresh_tokens_expires_at");

        builder.HasOne<RefreshToken>()
            .WithMany()
            .HasForeignKey(token => token.ReplacedByTokenId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
