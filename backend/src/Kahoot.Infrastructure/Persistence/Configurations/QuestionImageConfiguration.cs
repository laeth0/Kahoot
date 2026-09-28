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

        builder.HasIndex(image => new { image.Id, image.HostAccountId })
            .IsUnique()
            .HasDatabaseName("ux_question_images_id_host_account");

        builder.HasIndex(image => image.StoragePath)
            .IsUnique()
            .HasDatabaseName("ux_question_images_storage_path");

        builder.HasIndex(image => new { image.HostAccountId, image.CreatedAt, image.Id })
            .HasDatabaseName("ix_question_images_host_account_created_id");

        builder.HasIndex(image => image.UnreferencedSince)
            .HasDatabaseName("ix_question_images_orphan_cleanup");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(image => image.HostAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
