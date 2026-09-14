using Kahoot.Domain.Games;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kahoot.Infrastructure.Persistence.Configurations;

public sealed class AnswerSelectedChoiceConfiguration : IEntityTypeConfiguration<AnswerSelectedChoice>
{
    public void Configure(EntityTypeBuilder<AnswerSelectedChoice> builder)
    {
        builder.HasKey(selectedChoice => selectedChoice.Id);
        builder.Property(selectedChoice => selectedChoice.Id).ValueGeneratedNever();

        builder.HasIndex(selectedChoice => new { selectedChoice.AnswerId, selectedChoice.SelectedChoiceId })
            .IsUnique()
            .HasDatabaseName("uq_answer_selected_choice");

        builder.HasIndex(selectedChoice => selectedChoice.AnswerId)
            .HasDatabaseName("ix_answer_selected_choice_answer_id");

        builder.HasIndex(selectedChoice => selectedChoice.SelectedChoiceId)
            .HasDatabaseName("ix_answer_selected_choice_choice_id");

        builder.HasOne(selectedChoice => selectedChoice.Answer)
            .WithMany(answer => answer.SelectedChoices)
            .HasForeignKey(selectedChoice => selectedChoice.AnswerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(selectedChoice => selectedChoice.SelectedChoice)
            .WithMany(choice => choice.SelectedChoices)
            .HasForeignKey(selectedChoice => selectedChoice.SelectedChoiceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
