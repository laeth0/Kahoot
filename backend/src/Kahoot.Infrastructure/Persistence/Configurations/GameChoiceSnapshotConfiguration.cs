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

        // Composite Multi-Tenant Key - Supports composite foreign key references from answer submission choices
        builder.HasIndex(choice => new { choice.Id, choice.HostAccountId })
            .IsUnique()
            .HasDatabaseName("ux_game_choices_id_host_account");

        // Composite Multi-Tenant Question Key - Enforces three-way tenant partition (Choice, Host, Question)
        builder.HasIndex(choice => new { choice.Id, choice.HostAccountId, choice.GameQuestionId })
            .IsUnique()
            .HasDatabaseName("ux_game_choices_id_host_account_question");

        // Unique Choice Ordering Index - Enforces unique zero-based choice display sequence per question snapshot
        builder.HasIndex(choice => new { choice.GameQuestionId, choice.OrderIndex })
            .IsUnique()
            .HasDatabaseName("ux_game_choices_question_order");

        // Composite Multi-Tenant Foreign Key (TENANT-001) - Links choice snapshot strictly to parent question snapshot
        builder.HasOne<GameQuestionSnapshot>()
            .WithMany()
            .HasForeignKey(choice => new { choice.GameQuestionId, choice.HostAccountId })
            .HasPrincipalKey(question => new { question.Id, question.HostAccountId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
