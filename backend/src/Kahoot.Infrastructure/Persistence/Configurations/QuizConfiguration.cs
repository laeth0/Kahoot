using Kahoot.Domain.Quizzes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kahoot.Infrastructure.Persistence.Configurations;

public sealed class QuizConfiguration : IEntityTypeConfiguration<Quiz>
{
    public void Configure(EntityTypeBuilder<Quiz> builder)
    {
        builder.HasKey(quiz => quiz.Id);
        builder.Property(quiz => quiz.Id).ValueGeneratedNever();

        builder.Property(quiz => quiz.Title).HasMaxLength(200).IsRequired();
        builder.Property(quiz => quiz.Description).HasMaxLength(1000);

        builder.Property(quiz => quiz.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(quiz => quiz.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(quiz => quiz.HostId).HasDatabaseName("ix_quiz_host_id");

        builder.HasMany(quiz => quiz.Questions)
            .WithOne(question => question.Quiz)
            .HasForeignKey(question => question.QuizId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
