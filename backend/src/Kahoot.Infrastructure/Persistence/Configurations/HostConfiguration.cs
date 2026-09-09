using Kahoot.Domain.Hosts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kahoot.Infrastructure.Persistence.Configurations;

public sealed class HostConfiguration : IEntityTypeConfiguration<Host>
{
    public void Configure(EntityTypeBuilder<Host> builder)
    {
        builder.HasKey(host => host.Id);
        builder.Property(host => host.Id).ValueGeneratedNever();

        builder.Property(host => host.Username).HasMaxLength(64).IsRequired();
        builder.Property(host => host.PasswordHash).HasMaxLength(256).IsRequired();

        builder.Property(host => host.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(host => host.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(host => host.Username).IsUnique().HasDatabaseName("uq_host_username");

        builder.HasMany(host => host.Quizzes)
            .WithOne(quiz => quiz.Host)
            .HasForeignKey(quiz => quiz.HostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(host => host.RefreshTokens)
            .WithOne(token => token.Host)
            .HasForeignKey(token => token.HostId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
