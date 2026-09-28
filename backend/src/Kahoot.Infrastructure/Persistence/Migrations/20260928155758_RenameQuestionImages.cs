using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kahoot.Infrastructure.Persistence.Migrations;

public partial class RenameQuestionImages : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM questions
                    WHERE media_item_id IS NOT NULL
                    GROUP BY media_item_id
                    HAVING COUNT(*) > 1
                ) THEN
                    RAISE EXCEPTION 'Cannot enforce single question ownership: an image is attached to multiple questions.';
                END IF;
                IF EXISTS (SELECT 1 FROM media_items WHERE status <> 'active') THEN
                    RAISE EXCEPTION 'Cannot remove image status while deletion-pending records exist.';
                END IF;
            END $$;
            """);

        migrationBuilder.DropForeignKey(
            name: "fk_game_question_snapshots_media_items_media_item_id_host_acco",
            table: "game_question_snapshots");

        migrationBuilder.DropForeignKey(
            name: "fk_questions_media_items_media_item_id_host_account_id",
            table: "questions");

        migrationBuilder.DropIndex(
            name: "ix_questions_media_item_id_host_account_id",
            table: "questions");

        migrationBuilder.RenameTable(name: "media_items", newName: "question_images");
        migrationBuilder.Sql("""
            ALTER TABLE question_images RENAME CONSTRAINT pk_media_items TO pk_question_images;
            ALTER TABLE question_images RENAME CONSTRAINT ak_media_items_id_host_account_id TO ak_question_images_id_host_account_id;
            ALTER TABLE question_images RENAME CONSTRAINT fk_media_items_users_host_account_id TO fk_question_images_users_host_account_id;
            """);

        migrationBuilder.RenameColumn(name: "media_item_id", table: "questions", newName: "image_id");
        migrationBuilder.RenameColumn(name: "media_item_id", table: "game_question_snapshots", newName: "image_id");
        migrationBuilder.RenameColumn(name: "media_url", table: "game_question_snapshots", newName: "image_url");

        migrationBuilder.RenameIndex(
            name: "ix_game_question_snapshots_media_item_id_host_account_id",
            table: "game_question_snapshots",
            newName: "ix_game_question_snapshots_image_id_host_account_id");
        migrationBuilder.RenameIndex(name: "ux_media_items_id_host_account", table: "question_images", newName: "ux_question_images_id_host_account");
        migrationBuilder.RenameIndex(name: "ux_media_items_storage_path", table: "question_images", newName: "ux_question_images_storage_path");
        migrationBuilder.RenameIndex(name: "ix_media_items_host_account_created_id", table: "question_images", newName: "ix_question_images_host_account_created_id");
        migrationBuilder.DropIndex(name: "ix_media_items_orphan_cleanup", table: "question_images");

        migrationBuilder.Sql("""
            UPDATE question_images AS image
            SET unreferenced_since = NOW()
            WHERE unreferenced_since IS NULL
              AND NOT EXISTS (SELECT 1 FROM questions WHERE image_id = image.id)
              AND NOT EXISTS (SELECT 1 FROM game_question_snapshots WHERE image_id = image.id);
            """);

        migrationBuilder.DropColumn(name: "reference_count", table: "question_images");
        migrationBuilder.DropColumn(name: "status", table: "question_images");

        migrationBuilder.AlterDatabase()
            .Annotation("Npgsql:Enum:game_status", "created,finished,leaderboard,lobby,question_active,question_results")
            .Annotation("Npgsql:Enum:user_role", "host,system_admin")
            .Annotation("Npgsql:Enum:user_status", "active,suspended")
            .OldAnnotation("Npgsql:Enum:game_status", "created,finished,leaderboard,lobby,question_active,question_results")
            .OldAnnotation("Npgsql:Enum:media_status", "active,deletion_pending")
            .OldAnnotation("Npgsql:Enum:user_role", "host,system_admin")
            .OldAnnotation("Npgsql:Enum:user_status", "active,suspended");

        migrationBuilder.CreateIndex(
            name: "ix_question_images_orphan_cleanup",
            table: "question_images",
            column: "unreferenced_since");
        migrationBuilder.CreateIndex(
            name: "ix_questions_image_id_host_account_id",
            table: "questions",
            columns: new[] { "image_id", "host_account_id" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "ux_questions_image_id",
            table: "questions",
            column: "image_id",
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "fk_game_question_snapshots_question_images_image_id_host_accou",
            table: "game_question_snapshots",
            columns: new[] { "image_id", "host_account_id" },
            principalTable: "question_images",
            principalColumns: new[] { "id", "host_account_id" },
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: "fk_questions_question_images_image_id_host_account_id",
            table: "questions",
            columns: new[] { "image_id", "host_account_id" },
            principalTable: "question_images",
            principalColumns: new[] { "id", "host_account_id" },
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_game_question_snapshots_question_images_image_id_host_accou",
            table: "game_question_snapshots");
        migrationBuilder.DropForeignKey(
            name: "fk_questions_question_images_image_id_host_account_id",
            table: "questions");
        migrationBuilder.DropIndex(name: "ix_questions_image_id_host_account_id", table: "questions");
        migrationBuilder.DropIndex(name: "ux_questions_image_id", table: "questions");
        migrationBuilder.DropIndex(name: "ix_question_images_orphan_cleanup", table: "question_images");

        migrationBuilder.AlterDatabase()
            .Annotation("Npgsql:Enum:game_status", "created,finished,leaderboard,lobby,question_active,question_results")
            .Annotation("Npgsql:Enum:media_status", "active,deletion_pending")
            .Annotation("Npgsql:Enum:user_role", "host,system_admin")
            .Annotation("Npgsql:Enum:user_status", "active,suspended")
            .OldAnnotation("Npgsql:Enum:game_status", "created,finished,leaderboard,lobby,question_active,question_results")
            .OldAnnotation("Npgsql:Enum:user_role", "host,system_admin")
            .OldAnnotation("Npgsql:Enum:user_status", "active,suspended");

        migrationBuilder.Sql("""
            ALTER TABLE question_images ADD COLUMN status media_status NOT NULL DEFAULT 'active';
            ALTER TABLE question_images ADD COLUMN reference_count integer NOT NULL DEFAULT 0;
            """);
        migrationBuilder.RenameColumn(name: "image_id", table: "questions", newName: "media_item_id");
        migrationBuilder.RenameColumn(name: "image_id", table: "game_question_snapshots", newName: "media_item_id");
        migrationBuilder.RenameColumn(name: "image_url", table: "game_question_snapshots", newName: "media_url");
        migrationBuilder.RenameIndex(
            name: "ix_game_question_snapshots_image_id_host_account_id",
            table: "game_question_snapshots",
            newName: "ix_game_question_snapshots_media_item_id_host_account_id");
        migrationBuilder.RenameIndex(name: "ux_question_images_id_host_account", table: "question_images", newName: "ux_media_items_id_host_account");
        migrationBuilder.RenameIndex(name: "ux_question_images_storage_path", table: "question_images", newName: "ux_media_items_storage_path");
        migrationBuilder.RenameIndex(name: "ix_question_images_host_account_created_id", table: "question_images", newName: "ix_media_items_host_account_created_id");
        migrationBuilder.RenameTable(name: "question_images", newName: "media_items");
        migrationBuilder.Sql("""
            ALTER TABLE media_items RENAME CONSTRAINT pk_question_images TO pk_media_items;
            ALTER TABLE media_items RENAME CONSTRAINT ak_question_images_id_host_account_id TO ak_media_items_id_host_account_id;
            ALTER TABLE media_items RENAME CONSTRAINT fk_question_images_users_host_account_id TO fk_media_items_users_host_account_id;
            UPDATE media_items AS image
            SET reference_count = (SELECT COUNT(*) FROM questions WHERE media_item_id = image.id);
            """);

        migrationBuilder.CreateIndex(
            name: "ix_media_items_orphan_cleanup",
            table: "media_items",
            columns: new[] { "status", "reference_count", "unreferenced_since" });
        migrationBuilder.CreateIndex(
            name: "ix_questions_media_item_id_host_account_id",
            table: "questions",
            columns: new[] { "media_item_id", "host_account_id" });
        migrationBuilder.AddForeignKey(
            name: "fk_game_question_snapshots_media_items_media_item_id_host_acco",
            table: "game_question_snapshots",
            columns: new[] { "media_item_id", "host_account_id" },
            principalTable: "media_items",
            principalColumns: new[] { "id", "host_account_id" },
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: "fk_questions_media_items_media_item_id_host_account_id",
            table: "questions",
            columns: new[] { "media_item_id", "host_account_id" },
            principalTable: "media_items",
            principalColumns: new[] { "id", "host_account_id" },
            onDelete: ReferentialAction.Restrict);
    }
}
