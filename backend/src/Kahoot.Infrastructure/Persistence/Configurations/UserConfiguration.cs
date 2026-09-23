using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kahoot.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(user => user.Id);

        builder.Property(user => user.Username)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(user => user.NormalizedUsername)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(user => user.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(user => user.Name)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(user => user.PasswordHash)
            .IsRequired();

        builder.Property(user => user.Role)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(UserRole.Host)
            .HasSentinel((UserRole)0)
            .IsRequired();

        builder.Property(user => user.Status)
            .HasMaxLength(32)
            .HasDefaultValue("Active")
            .IsRequired();

        builder.Property(user => user.TokenSecurityVersion)
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(user => user.CreatedAt)
            .IsRequired();

        builder.Property(user => user.UpdatedAt)
            .IsRequired();

        builder.HasIndex(user => user.NormalizedUsername)
            .IsUnique();

        builder.HasIndex(user => user.Email)
            .IsUnique();
    }
}
