using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kahoot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameQuestionImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_game_question_snapshots_media_items_media_item_id_host_acco",
                table: "game_question_snapshots");

            migrationBuilder.DropForeignKey(
                name: "fk_questions_media_items_media_item_id_host_account_id",
                table: "questions");

            migrationBuilder.DropTable(
                name: "media_items");

            migrationBuilder.DropIndex(
                name: "ix_questions_media_item_id_host_account_id",
                table: "questions");

            migrationBuilder.RenameColumn(
                name: "media_item_id",
                table: "questions",
                newName: "image_id");

            migrationBuilder.RenameColumn(
                name: "media_url",
                table: "game_question_snapshots",
                newName: "image_url");

            migrationBuilder.RenameColumn(
                name: "media_item_id",
                table: "game_question_snapshots",
                newName: "image_id");

            migrationBuilder.RenameIndex(
                name: "ix_game_question_snapshots_media_item_id_host_account_id",
                table: "game_question_snapshots",
                newName: "ix_game_question_snapshots_image_id_host_account_id");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:game_status", "created,finished,leaderboard,lobby,question_active,question_results")
                .Annotation("Npgsql:Enum:user_role", "host,system_admin")
                .Annotation("Npgsql:Enum:user_status", "active,suspended")
                .OldAnnotation("Npgsql:Enum:game_status", "created,finished,leaderboard,lobby,question_active,question_results")
                .OldAnnotation("Npgsql:Enum:media_status", "active,deletion_pending")
                .OldAnnotation("Npgsql:Enum:user_role", "host,system_admin")
                .OldAnnotation("Npgsql:Enum:user_status", "active,suspended");

            migrationBuilder.CreateTable(
                name: "question_images",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    host_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    content_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    byte_size = table.Column<long>(type: "bigint", nullable: false),
                    pixel_width = table.Column<int>(type: "integer", nullable: false),
                    pixel_height = table.Column<int>(type: "integer", nullable: false),
                    unreferenced_since = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_question_images", x => x.id);
                    table.UniqueConstraint("ak_question_images_id_host_account_id", x => new { x.id, x.host_account_id });
                    table.ForeignKey(
                        name: "fk_question_images_users_host_account_id",
                        column: x => x.host_account_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

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

            migrationBuilder.CreateIndex(
                name: "ix_question_images_host_account_created_id",
                table: "question_images",
                columns: new[] { "host_account_id", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_question_images_orphan_cleanup",
                table: "question_images",
                column: "unreferenced_since");

            migrationBuilder.CreateIndex(
                name: "ux_question_images_id_host_account",
                table: "question_images",
                columns: new[] { "id", "host_account_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_question_images_storage_path",
                table: "question_images",
                column: "storage_path",
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_game_question_snapshots_question_images_image_id_host_accou",
                table: "game_question_snapshots");

            migrationBuilder.DropForeignKey(
                name: "fk_questions_question_images_image_id_host_account_id",
                table: "questions");

            migrationBuilder.DropTable(
                name: "question_images");

            migrationBuilder.DropIndex(
                name: "ix_questions_image_id_host_account_id",
                table: "questions");

            migrationBuilder.DropIndex(
                name: "ux_questions_image_id",
                table: "questions");

            migrationBuilder.RenameColumn(
                name: "image_id",
                table: "questions",
                newName: "media_item_id");

            migrationBuilder.RenameColumn(
                name: "image_url",
                table: "game_question_snapshots",
                newName: "media_url");

            migrationBuilder.RenameColumn(
                name: "image_id",
                table: "game_question_snapshots",
                newName: "media_item_id");

            migrationBuilder.RenameIndex(
                name: "ix_game_question_snapshots_image_id_host_account_id",
                table: "game_question_snapshots",
                newName: "ix_game_question_snapshots_media_item_id_host_account_id");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:game_status", "created,finished,leaderboard,lobby,question_active,question_results")
                .Annotation("Npgsql:Enum:media_status", "active,deletion_pending")
                .Annotation("Npgsql:Enum:user_role", "host,system_admin")
                .Annotation("Npgsql:Enum:user_status", "active,suspended")
                .OldAnnotation("Npgsql:Enum:game_status", "created,finished,leaderboard,lobby,question_active,question_results")
                .OldAnnotation("Npgsql:Enum:user_role", "host,system_admin")
                .OldAnnotation("Npgsql:Enum:user_status", "active,suspended");

            migrationBuilder.CreateTable(
                name: "media_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    byte_size = table.Column<long>(type: "bigint", nullable: false),
                    content_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    host_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pixel_height = table.Column<int>(type: "integer", nullable: false),
                    pixel_width = table.Column<int>(type: "integer", nullable: false),
                    reference_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    status = table.Column<int>(type: "media_status", nullable: false),
                    storage_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    unreferenced_since = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media_items", x => x.id);
                    table.UniqueConstraint("ak_media_items_id_host_account_id", x => new { x.id, x.host_account_id });
                    table.ForeignKey(
                        name: "fk_media_items_users_host_account_id",
                        column: x => x.host_account_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_questions_media_item_id_host_account_id",
                table: "questions",
                columns: new[] { "media_item_id", "host_account_id" });

            migrationBuilder.CreateIndex(
                name: "ix_media_items_host_account_created_id",
                table: "media_items",
                columns: new[] { "host_account_id", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_media_items_orphan_cleanup",
                table: "media_items",
                columns: new[] { "status", "reference_count", "unreferenced_since" });

            migrationBuilder.CreateIndex(
                name: "ux_media_items_id_host_account",
                table: "media_items",
                columns: new[] { "id", "host_account_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_media_items_storage_path",
                table: "media_items",
                column: "storage_path",
                unique: true);

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
}
