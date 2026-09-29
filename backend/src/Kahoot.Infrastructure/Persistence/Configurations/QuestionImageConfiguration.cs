using Kahoot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kahoot.Infrastructure.Persistence.Configurations;

public sealed class QuestionImageConfiguration : IEntityTypeConfiguration<QuestionImage>
{
    public void Configure(EntityTypeBuilder<QuestionImage> builder)
    {
        builder.ToTable("question_images");

        builder.HasKey(image => image.Id);

        builder.Property(image => image.HostAccountId)
            .IsRequired();

        builder.Property(image => image.StoragePath)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(image => image.ContentType)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(image => image.ByteSize)
            .IsRequired();

        builder.Property(image => image.PixelWidth)
            .IsRequired();

        builder.Property(image => image.PixelHeight)
            .IsRequired();

        builder.Property(image => image.UnreferencedSince);

        builder.Property(image => image.CreatedAt)
            .IsRequired();

        // Composite Multi-Tenant Key - Supports composite foreign key references from questions and game snapshots
        builder.HasIndex(image => new { image.Id, image.HostAccountId })
            .IsUnique()
            .HasDatabaseName("ux_question_images_id_host_account");

        // Unique Storage Path Index (IMG-STOR-001) - Guarantees relative storage path uniqueness across disk assets
        builder.HasIndex(image => image.StoragePath)
            .IsUnique()
            .HasDatabaseName("ux_question_images_storage_path");

        // Keyset Pagination Index - Supports host image gallery listing sorted by creation date
        builder.HasIndex(image => new { image.HostAccountId, image.CreatedAt, image.Id })
            .HasDatabaseName("ix_question_images_host_account_created_id");

        // Background Cleanup Worker Index (IMG-LIFE-001) - Optimizes background orphan sweeps filtering by UnreferencedSince timestamp
        builder.HasIndex(image => image.UnreferencedSince)
            .HasDatabaseName("ix_question_images_orphan_cleanup");

        // Referential Integrity Constraint - Restricts deletion of host User while image assets remain
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(image => image.HostAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
