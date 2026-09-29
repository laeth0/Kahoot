using Kahoot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kahoot.Infrastructure.Persistence.Configurations;

public sealed class QuizConfiguration : IEntityTypeConfiguration<Quiz>
{
    public void Configure(EntityTypeBuilder<Quiz> builder)
    {
        builder.ToTable("quizzes");

        builder.HasKey(quiz => quiz.Id);

        builder.Property(quiz => quiz.HostAccountId)
            .IsRequired();

        builder.Property(quiz => quiz.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(quiz => quiz.Description)
            .HasMaxLength(1000);

        builder.Property(quiz => quiz.Revision)
            .HasDefaultValue(1L)
            .IsRequired();

        builder.Property(quiz => quiz.CreatedAt)
            .IsRequired();

        builder.Property(quiz => quiz.UpdatedAt)
            .IsRequired();

        builder.Property(quiz => quiz.CreatedBy)
            .IsRequired(false);

        builder.Property(quiz => quiz.UpdatedBy)
            .IsRequired(false);

        builder.HasIndex(quiz => new { quiz.Id, quiz.HostAccountId })
            .IsUnique()
            .HasDatabaseName("ux_quizzes_id_host_account");

        builder.HasIndex(quiz => new { quiz.HostAccountId, quiz.CreatedAt, quiz.Id })
            .HasDatabaseName("ix_quizzes_host_account_created_id");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(quiz => quiz.HostAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
