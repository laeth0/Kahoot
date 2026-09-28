using Kahoot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kahoot.Infrastructure.Persistence.Configurations;

public sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("questions");

        builder.HasKey(question => question.Id);

        builder.Property(question => question.HostAccountId)
            .IsRequired();

        builder.Property(question => question.QuizId)
            .IsRequired();

        builder.Property(question => question.Text)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(question => question.ImageId);

        builder.Property(question => question.DurationSeconds)
            .IsRequired();

        builder.Property(question => question.BasePoints)
            .IsRequired();

        builder.Property(question => question.OrderIndex)
            .IsRequired();

        builder.HasIndex(question => new { question.Id, question.HostAccountId })
            .IsUnique()
            .HasDatabaseName("ux_questions_id_host_account");

        builder.HasIndex(question => question.ImageId)
            .IsUnique()
            .HasDatabaseName("ux_questions_image_id");

        builder.HasIndex(question => new { question.QuizId, question.OrderIndex })
            .IsUnique()
            .HasDatabaseName("ux_questions_quiz_order");

        builder.HasIndex(question => new { question.HostAccountId, question.QuizId, question.OrderIndex })
            .HasDatabaseName("ix_questions_host_account_quiz_order");

        builder.HasOne<Quiz>()
            .WithMany()
            .HasForeignKey(question => new { question.QuizId, question.HostAccountId })
            .HasPrincipalKey(quiz => new { quiz.Id, quiz.HostAccountId })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(question => question.Image)
            .WithOne()
            .HasForeignKey<Question>(question => new { question.ImageId, question.HostAccountId })
            .HasPrincipalKey<QuestionImage>(image => new { image.Id, image.HostAccountId })
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
