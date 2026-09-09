using Kahoot.Domain.Quizzes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kahoot.Infrastructure.Persistence.Configurations;

public sealed class ChoiceConfiguration : IEntityTypeConfiguration<Choice>
{
    public void Configure(EntityTypeBuilder<Choice> builder)
    {
        builder.HasKey(choice => choice.Id);
        builder.Property(choice => choice.Id).ValueGeneratedNever();

        builder.Property(choice => choice.Text).HasMaxLength(300).IsRequired();
        builder.Property(choice => choice.IsCorrect).HasDefaultValue(false);

        builder.Property(choice => choice.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(choice => choice.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(choice => new { choice.QuestionId, choice.OrderIndex })
            .IsUnique()
            .HasDatabaseName("uq_choice_question_order");
        builder.HasIndex(choice => choice.QuestionId).HasDatabaseName("ix_choice_question_id");
    }
}
