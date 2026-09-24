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

        builder.HasIndex(choice => new { choice.Id, choice.HostAccountId })
            .IsUnique()
            .HasDatabaseName("ux_choices_id_host_account");

        builder.HasIndex(choice => new { choice.QuestionId, choice.OrderIndex })
            .IsUnique()
            .HasDatabaseName("ux_choices_question_order");

        builder.HasIndex(choice => new { choice.HostAccountId, choice.QuestionId, choice.OrderIndex })
            .HasDatabaseName("ix_choices_host_account_question_order");

        builder.HasOne<Question>()
            .WithMany()
            .HasForeignKey(choice => new { choice.QuestionId, choice.HostAccountId })
            .HasPrincipalKey(question => new { question.Id, question.HostAccountId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
