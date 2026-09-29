using Kahoot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kahoot.Infrastructure.Persistence.Configurations;

public sealed class AnswerSubmissionChoiceConfiguration : IEntityTypeConfiguration<AnswerSubmissionChoice>
{
    public void Configure(EntityTypeBuilder<AnswerSubmissionChoice> builder)
    {
        builder.ToTable("answer_submission_choices");

        // Composite Primary Key - Prevents duplicate selection of the same choice in a single submission
        builder.HasKey(choice => new { choice.AnswerSubmissionId, choice.GameChoiceId })
            .HasName("pk_answer_submission_choices");

        builder.Property(choice => choice.AnswerSubmissionId)
            .IsRequired();

        builder.Property(choice => choice.HostAccountId)
            .IsRequired();

        builder.Property(choice => choice.GameQuestionId)
            .IsRequired();

        builder.Property(choice => choice.GameChoiceId)
            .IsRequired();

        // Choice Distribution Index (PLAY-STAT-001) - Optimizes aggregation of participant selections per question option
        builder.HasIndex(choice => new { choice.GameQuestionId, choice.GameChoiceId })
            .HasDatabaseName("ix_answer_submission_choices_question_choice");

        // Composite Multi-Tenant Foreign Key (TENANT-001) - Links choice selection strictly to parent answer submission
        builder.HasOne<AnswerSubmission>()
            .WithMany()
            .HasForeignKey(choice => new { choice.AnswerSubmissionId, choice.HostAccountId, choice.GameQuestionId })
            .HasPrincipalKey(submission => new { submission.Id, submission.HostAccountId, submission.GameQuestionId })
            .OnDelete(DeleteBehavior.Restrict);

        // Composite Multi-Tenant Foreign Key (TENANT-001) - Links choice selection strictly to immutable choice snapshot
        builder.HasOne<GameChoiceSnapshot>()
            .WithMany()
            .HasForeignKey(choice => new { choice.GameChoiceId, choice.HostAccountId, choice.GameQuestionId })
            .HasPrincipalKey(snapshot => new { snapshot.Id, snapshot.HostAccountId, snapshot.GameQuestionId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
