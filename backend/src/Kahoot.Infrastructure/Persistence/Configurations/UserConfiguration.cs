using Kahoot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kahoot.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // Relational Table Mapping - Maps user entity to PostgreSQL 'users' table
        builder.ToTable("users");

        // Primary Key - Enforces unique UUID primary key clustered by default
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

        // Token Security Version Default - Initializes security barrier to 1
        builder.Property(user => user.TokenSecurityVersion)
            .HasDefaultValue(1)
            .IsRequired();

        // Monotonic Revision Counter Default - Initializes optimistic concurrency counter to 1
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

        // Unique Canonical Identifier Index - Guarantees global case-insensitive username uniqueness
        builder.HasIndex(user => user.NormalizedUsername)
            .IsUnique()
            .HasDatabaseName("ux_users_normalized_username");

        // Composite Index For Admin Queries - Optimizes keyset cursor pagination with Role and Status filter pushdown
        builder.HasIndex(user => new { user.Role, user.Status, user.CreatedAt, user.Id })
            .HasDatabaseName("ix_users_admin_listing");
    }
}
