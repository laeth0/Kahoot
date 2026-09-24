using Kahoot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kahoot.Infrastructure.Persistence.Configurations;

public sealed class MediaItemConfiguration : IEntityTypeConfiguration<MediaItem>
{
    public void Configure(EntityTypeBuilder<MediaItem> builder)
    {
        builder.ToTable("media_items");

        builder.HasKey(media => media.Id);

        builder.Property(media => media.HostAccountId)
            .IsRequired();

        builder.Property(media => media.StoragePath)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(media => media.ContentType)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(media => media.ByteSize)
            .IsRequired();

        builder.Property(media => media.PixelWidth)
            .IsRequired();

        builder.Property(media => media.PixelHeight)
            .IsRequired();

        builder.Property(media => media.Status)
            .IsRequired();

        builder.Property(media => media.ReferenceCount)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(media => media.UnreferencedSince);

        builder.Property(media => media.CreatedAt)
            .IsRequired();

        builder.HasIndex(media => new { media.Id, media.HostAccountId })
            .IsUnique()
            .HasDatabaseName("ux_media_items_id_host_account");

        builder.HasIndex(media => media.StoragePath)
            .IsUnique()
            .HasDatabaseName("ux_media_items_storage_path");

        builder.HasIndex(media => new { media.HostAccountId, media.CreatedAt, media.Id })
            .HasDatabaseName("ix_media_items_host_account_created_id");

        builder.HasIndex(media => new { media.Status, media.ReferenceCount, media.UnreferencedSince })
            .HasDatabaseName("ix_media_items_orphan_cleanup");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(media => media.HostAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
