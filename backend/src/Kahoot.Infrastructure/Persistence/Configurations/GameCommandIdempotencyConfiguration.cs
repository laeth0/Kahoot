using Kahoot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kahoot.Infrastructure.Persistence.Configurations;

public sealed class GameCommandIdempotencyConfiguration : IEntityTypeConfiguration<GameCommandIdempotency>
{
    public void Configure(EntityTypeBuilder<GameCommandIdempotency> builder)
    {
        builder.ToTable("game_command_idempotency");

        builder.HasKey(command => new { command.GameId, command.CommandId })
            .HasName("pk_game_command_idempotency");

        builder.Property(command => command.GameId)
            .IsRequired();

        builder.Property(command => command.HostAccountId)
            .IsRequired();

        builder.Property(command => command.CommandId)
            .IsRequired();

        builder.Property(command => command.CommandName)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(command => command.RequestHash)
            .HasColumnType("bytea")
            .IsRequired();

        builder.Property(command => command.ResultStateVersion)
            .IsRequired();

        builder.Property(command => command.ResponsePayload)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(command => command.CreatedAt)
            .IsRequired();

        builder.HasIndex(command => new { command.HostAccountId, command.GameId, command.CreatedAt })
            .HasDatabaseName("ix_game_command_idempotency_host_account_game_time");

        builder.HasOne<Game>()
            .WithMany()
            .HasForeignKey(command => new { command.GameId, command.HostAccountId })
            .HasPrincipalKey(game => new { game.Id, game.HostAccountId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
