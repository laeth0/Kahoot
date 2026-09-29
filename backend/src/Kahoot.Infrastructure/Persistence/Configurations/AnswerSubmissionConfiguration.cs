using Kahoot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kahoot.Infrastructure.Persistence.Configurations;

public sealed class AnswerSubmissionConfiguration : IEntityTypeConfiguration<AnswerSubmission>
{
    public void Configure(EntityTypeBuilder<AnswerSubmission> builder)
    {
        builder.ToTable("answer_submissions");

        builder.HasKey(submission => submission.Id);

        builder.Property(submission => submission.HostAccountId)
            .IsRequired();

        builder.Property(submission => submission.GameId)
            .IsRequired();

        builder.Property(submission => submission.GameQuestionId)
            .IsRequired();

        builder.Property(submission => submission.ParticipantId)
            .IsRequired();

        builder.Property(submission => submission.SubmittedAt)
            .IsRequired();

        builder.Property(submission => submission.ResponseTimeMs)
            .IsRequired();

        builder.Property(submission => submission.IsCorrect)
            .IsRequired();

        builder.Property(submission => submission.PointsAwarded)
            .IsRequired();

        // Composite Multi-Tenant Key - Supports composite foreign key references from submission choices
        builder.HasIndex(submission => new { submission.Id, submission.HostAccountId, submission.GameQuestionId })
            .IsUnique()
            .HasDatabaseName("ux_answer_submissions_id_host_account_question");

        // Single-Submission Invariant (PLAY-SUB-001) - Enforces exactly one answer submission per participant per question snapshot
        builder.HasIndex(submission => new { submission.GameId, submission.GameQuestionId, submission.ParticipantId })
            .IsUnique()
            .HasDatabaseName("ux_answer_submissions_once_per_question");

        // Temporal Aggregation Index - Optimizes question result aggregation ordered by submission timestamp
        builder.HasIndex(submission => new { submission.GameQuestionId, submission.SubmittedAt })
            .HasDatabaseName("ix_answer_submissions_question_time");

        // Composite Multi-Tenant Foreign Key (TENANT-001) - Links submission strictly to the live game session
        builder.HasOne<Game>()
            .WithMany()
            .HasForeignKey(submission => new { submission.GameId, submission.HostAccountId })
            .HasPrincipalKey(game => new { game.Id, game.HostAccountId })
            .OnDelete(DeleteBehavior.Restrict);

        // Composite Multi-Tenant Foreign Key (TENANT-001) - Links submission strictly to the question snapshot
        builder.HasOne<GameQuestionSnapshot>()
            .WithMany()
            .HasForeignKey(submission => new { submission.GameQuestionId, submission.HostAccountId, submission.GameId })
            .HasPrincipalKey(question => new { question.Id, question.HostAccountId, question.GameId })
            .OnDelete(DeleteBehavior.Restrict);

        // Composite Multi-Tenant Foreign Key (TENANT-001) - Links submission strictly to the submitting participant
        builder.HasOne<Participant>()
            .WithMany()
            .HasForeignKey(submission => new { submission.ParticipantId, submission.HostAccountId, submission.GameId })
            .HasPrincipalKey(participant => new { participant.Id, participant.HostAccountId, participant.GameId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
