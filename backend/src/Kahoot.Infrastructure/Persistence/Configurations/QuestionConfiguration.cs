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

        // Composite Multi-Tenant Key - Supports composite foreign key references from child choices
        builder.HasIndex(question => new { question.Id, question.HostAccountId })
            .IsUnique()
            .HasDatabaseName("ux_questions_id_host_account");

        // Unique Permutation Index (QUIZ-REORDER-001) - Enforces strictly unique zero-based ordering index per quiz
        builder.HasIndex(question => new { question.QuizId, question.OrderIndex })
            .IsUnique()
            .HasDatabaseName("ux_questions_quiz_order");

        // Composite Index For Questions Seek - Optimizes sequential fetching of questions by host and quiz
        builder.HasIndex(question => new { question.HostAccountId, question.QuizId, question.OrderIndex })
            .HasDatabaseName("ix_questions_host_account_quiz_order");

        // Composite Multi-Tenant Foreign Key (TENANT-001) - Ensures question belongs strictly to the parent quiz's host tenant
        builder.HasOne<Quiz>()
            .WithMany()
            .HasForeignKey(question => new { question.QuizId, question.HostAccountId })
            .HasPrincipalKey(quiz => new { quiz.Id, quiz.HostAccountId })
            .OnDelete(DeleteBehavior.Cascade);

        // Cross-Tenant Image Isolation & Single-Question Attachment (QUIZ-SEC-001, IMG-ATT-001) - Restricts image ownership to host tenant
        builder.HasOne(question => question.Image)
            .WithOne()
            .HasForeignKey<Question>(question => new { question.ImageId, question.HostAccountId })
            .HasPrincipalKey<QuestionImage>(image => new { image.Id, image.HostAccountId })
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
