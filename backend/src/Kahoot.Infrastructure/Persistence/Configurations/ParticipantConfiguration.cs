using Kahoot.Domain.Games;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kahoot.Infrastructure.Persistence.Configurations;

public sealed class ParticipantConfiguration : IEntityTypeConfiguration<Participant>
{
    public void Configure(EntityTypeBuilder<Participant> builder)
    {
        builder.HasKey(participant => participant.Id);
        builder.Property(participant => participant.Id).ValueGeneratedNever();

        builder.Property(participant => participant.Nickname).HasMaxLength(30).IsRequired();
        builder.Property(participant => participant.NicknameNormalized).HasMaxLength(30).IsRequired();
        builder.Property(participant => participant.SessionTokenHash).HasMaxLength(128).IsRequired();
        builder.Property(participant => participant.ConnectionId).HasMaxLength(128);
        builder.Property(participant => participant.TotalScore).HasDefaultValue(0);
        builder.Property(participant => participant.IsRemoved).HasDefaultValue(false);

        builder.Property(participant => participant.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(participant => participant.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(participant => participant.SessionTokenHash)
            .IsUnique()
            .HasDatabaseName("uq_participant_session_token");
        builder.HasIndex(participant => new { participant.GameSessionId, participant.NicknameNormalized })
            .IsUnique()
            .HasFilter("is_removed = false")
            .HasDatabaseName("uq_participant_game_nickname");
        builder.HasIndex(participant => participant.GameSessionId)
            .HasDatabaseName("ix_participant_game_session_id");
        builder.HasIndex(participant => new { participant.GameSessionId, participant.TotalScore })
            .HasDatabaseName("ix_participant_game_score");
        builder.HasIndex(participant => participant.ConnectionId)
            .HasDatabaseName("ix_participant_connection_id");

        builder.HasMany(participant => participant.Answers)
            .WithOne(answer => answer.Participant)
            .HasForeignKey(answer => answer.ParticipantId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.ToTable(table => table.HasCheckConstraint("ck_participant_total_score", "total_score >= 0"));
    }
}
