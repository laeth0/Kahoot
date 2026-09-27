using Kahoot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kahoot.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.DisplayUsername)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(user => user.NormalizedUsername)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(user => user.PasswordHash)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(user => user.Role)
            .IsRequired();

        builder.Property(user => user.Status)
            .IsRequired();

        builder.Property(user => user.TokenSecurityVersion)
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(user => user.Revision)
            .HasDefaultValue(1L)
            .IsRequired();

        builder.Property(user => user.TerminationPending)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(user => user.CreatedAt)
            .IsRequired();

        builder.Property(user => user.UpdatedAt)
            .IsRequired();

        builder.Property(user => user.CreatedBy)
            .IsRequired(false);

        builder.Property(user => user.UpdatedBy)
            .IsRequired(false);

        builder.HasIndex(user => user.NormalizedUsername)
            .IsUnique()
            .HasDatabaseName("ux_users_normalized_username");

        builder.HasIndex(user => new { user.Role, user.Status, user.CreatedAt, user.Id })
            .HasDatabaseName("ix_users_admin_listing");
    }
}
