using Kahoot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kahoot.Infrastructure.Persistence.Configurations;

public sealed class ChoiceConfiguration : IEntityTypeConfiguration<Choice>
{
    public void Configure(EntityTypeBuilder<Choice> builder)
    {
        builder.ToTable("choices");

        builder.HasKey(choice => choice.Id);

        builder.Property(choice => choice.HostAccountId)
            .IsRequired();

        builder.Property(choice => choice.QuestionId)
            .IsRequired();

        builder.Property(choice => choice.Text)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(choice => choice.IsCorrect)
            .IsRequired();

        builder.Property(choice => choice.OrderIndex)
            .IsRequired();

        // Composite Multi-Tenant Key - Supports composite foreign key references from submission choices
        builder.HasIndex(choice => new { choice.Id, choice.HostAccountId })
            .IsUnique()
            .HasDatabaseName("ux_choices_id_host_account");

        // Unique Choice Index (QUIZ-QUEST-004) - Enforces contiguous unique display order index per question
        builder.HasIndex(choice => new { choice.QuestionId, choice.OrderIndex })
            .IsUnique()
            .HasDatabaseName("ux_choices_question_order");

        // Composite Index For Choices Seek - Optimizes batch loading of choices by question
        builder.HasIndex(choice => new { choice.HostAccountId, choice.QuestionId, choice.OrderIndex })
            .HasDatabaseName("ix_choices_host_account_question_order");

        // Composite Multi-Tenant Foreign Key (TENANT-001) - Scopes choices strictly to parent question's host tenant
        builder.HasOne<Question>()
            .WithMany()
            .HasForeignKey(choice => new { choice.QuestionId, choice.HostAccountId })
            .HasPrincipalKey(question => new { question.Id, question.HostAccountId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
