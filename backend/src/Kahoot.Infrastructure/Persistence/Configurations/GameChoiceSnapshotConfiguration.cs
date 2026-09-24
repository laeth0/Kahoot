using Kahoot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kahoot.Infrastructure.Persistence.Configurations;

public sealed class GameChoiceSnapshotConfiguration : IEntityTypeConfiguration<GameChoiceSnapshot>
{
    public void Configure(EntityTypeBuilder<GameChoiceSnapshot> builder)
    {
        builder.ToTable("game_choice_snapshots");

        builder.HasKey(choice => choice.Id);

        builder.Property(choice => choice.HostAccountId)
            .IsRequired();

        builder.Property(choice => choice.GameQuestionId)
            .IsRequired();

        builder.Property(choice => choice.Text)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(choice => choice.IsCorrect)
            .IsRequired();

        builder.Property(choice => choice.OrderIndex)
            .IsRequired();

        builder.Property(choice => choice.SelectionCount)
            .HasDefaultValue(0)
            .IsRequired();

        builder.HasIndex(choice => new { choice.Id, choice.HostAccountId })
            .IsUnique()
            .HasDatabaseName("ux_game_choices_id_host_account");

        builder.HasIndex(choice => new { choice.Id, choice.HostAccountId, choice.GameQuestionId })
            .IsUnique()
            .HasDatabaseName("ux_game_choices_id_host_account_question");

        builder.HasIndex(choice => new { choice.GameQuestionId, choice.OrderIndex })
            .IsUnique()
            .HasDatabaseName("ux_game_choices_question_order");

        builder.HasOne<GameQuestionSnapshot>()
            .WithMany()
            .HasForeignKey(choice => new { choice.GameQuestionId, choice.HostAccountId })
            .HasPrincipalKey(question => new { question.Id, question.HostAccountId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
