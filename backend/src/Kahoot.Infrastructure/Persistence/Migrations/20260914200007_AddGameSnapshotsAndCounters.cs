using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kahoot.Infrastructure.Persistence.Migrations
{
    public partial class AddGameSnapshotsAndCounters : Migration
    {
            protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_answers_choices_selected_choice_id",
                table: "answers");

            migrationBuilder.DropForeignKey(
                name: "fk_answers_questions_question_id",
                table: "answers");

            migrationBuilder.DropForeignKey(
                name: "fk_game_sessions_questions_current_question_id",
                table: "game_sessions");

            migrationBuilder.AddColumn<int>(
                name: "current_question_answered_count",
                table: "game_sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "current_question_eligible_count",
                table: "game_sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "quiz_title",
                table: "game_sessions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "game_question_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_question_id = table.Column<Guid>(type: "uuid", nullable: true),
                    order_index = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    image_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    time_limit_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 20),
                    points = table.Column<int>(type: "integer", nullable: false, defaultValue: 1000)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_question_snapshots", x => x.id);
                    table.CheckConstraint("ck_game_question_snapshot_points", "points >= 0");
                    table.CheckConstraint("ck_game_question_snapshot_time_limit_seconds", "time_limit_seconds >= 5 AND time_limit_seconds <= 300");
                    table.ForeignKey(
                        name: "fk_game_question_snapshots_game_sessions_game_session_id",
                        column: x => x.game_session_id,
                        principalTable: "game_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "game_choice_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_choice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    order_index = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    image_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    is_correct = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_choice_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "fk_game_choice_snapshots_game_question_snapshots_question_snap",
                        column: x => x.question_snapshot_id,
                        principalTable: "game_question_snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_game_choice_snapshot_question_id",
                table: "game_choice_snapshots",
                column: "question_snapshot_id");

            migrationBuilder.CreateIndex(
                name: "uq_game_choice_snapshot_order",
                table: "game_choice_snapshots",
                columns: new[] { "question_snapshot_id", "order_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_game_question_snapshot_session_id",
                table: "game_question_snapshots",
                column: "game_session_id");

            migrationBuilder.CreateIndex(
                name: "uq_game_question_snapshot_order",
                table: "game_question_snapshots",
                columns: new[] { "game_session_id", "order_index" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_answers_game_choice_snapshots_selected_choice_id",
                table: "answers",
                column: "selected_choice_id",
                principalTable: "game_choice_snapshots",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_answers_game_question_snapshots_question_id",
                table: "answers",
                column: "question_id",
                principalTable: "game_question_snapshots",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_game_sessions_game_question_snapshots_current_question_id",
                table: "game_sessions",
                column: "current_question_id",
                principalTable: "game_question_snapshots",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

            protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_answers_game_choice_snapshots_selected_choice_id",
                table: "answers");

            migrationBuilder.DropForeignKey(
                name: "fk_answers_game_question_snapshots_question_id",
                table: "answers");

            migrationBuilder.DropForeignKey(
                name: "fk_game_sessions_game_question_snapshots_current_question_id",
                table: "game_sessions");

            migrationBuilder.DropTable(
                name: "game_choice_snapshots");

            migrationBuilder.DropTable(
                name: "game_question_snapshots");

            migrationBuilder.DropColumn(
                name: "current_question_answered_count",
                table: "game_sessions");

            migrationBuilder.DropColumn(
                name: "current_question_eligible_count",
                table: "game_sessions");

            migrationBuilder.DropColumn(
                name: "quiz_title",
                table: "game_sessions");

            migrationBuilder.AddForeignKey(
                name: "fk_answers_choices_selected_choice_id",
                table: "answers",
                column: "selected_choice_id",
                principalTable: "choices",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_answers_questions_question_id",
                table: "answers",
                column: "question_id",
                principalTable: "questions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_game_sessions_questions_current_question_id",
                table: "game_sessions",
                column: "current_question_id",
                principalTable: "questions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
