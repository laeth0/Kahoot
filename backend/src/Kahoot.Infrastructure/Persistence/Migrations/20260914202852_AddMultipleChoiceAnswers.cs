using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kahoot.Infrastructure.Persistence.Migrations
{
    public partial class AddMultipleChoiceAnswers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "answer_selected_choices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    answer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    selected_choice_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_answer_selected_choices", x => x.id);
                    table.ForeignKey(
                        name: "fk_answer_selected_choices_answers_answer_id",
                        column: x => x.answer_id,
                        principalTable: "answers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_answer_selected_choices_game_choice_snapshots_selected_choi",
                        column: x => x.selected_choice_id,
                        principalTable: "game_choice_snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_answer_selected_choice_answer_id",
                table: "answer_selected_choices",
                column: "answer_id");

            migrationBuilder.CreateIndex(
                name: "ix_answer_selected_choice_choice_id",
                table: "answer_selected_choices",
                column: "selected_choice_id");

            migrationBuilder.CreateIndex(
                name: "uq_answer_selected_choice",
                table: "answer_selected_choices",
                columns: new[] { "answer_id", "selected_choice_id" },
                unique: true);

            migrationBuilder.Sql("INSERT INTO answer_selected_choices (id, answer_id, selected_choice_id) SELECT gen_random_uuid(), id, selected_choice_id FROM answers ON CONFLICT DO NOTHING;");

            migrationBuilder.DropForeignKey(
                name: "fk_answers_game_choice_snapshots_selected_choice_id",
                table: "answers");

            migrationBuilder.DropIndex(
                name: "ix_answer_selected_choice_id",
                table: "answers");

            migrationBuilder.DropColumn(
                name: "selected_choice_id",
                table: "answers");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "selected_choice_id",
                table: "answers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql("UPDATE answers a SET selected_choice_id = sc.selected_choice_id FROM answer_selected_choices sc WHERE sc.answer_id = a.id;");

            migrationBuilder.CreateIndex(
                name: "ix_answer_selected_choice_id",
                table: "answers",
                column: "selected_choice_id");

            migrationBuilder.AddForeignKey(
                name: "fk_answers_game_choice_snapshots_selected_choice_id",
                table: "answers",
                column: "selected_choice_id",
                principalTable: "game_choice_snapshots",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropTable(
                name: "answer_selected_choices");
        }
    }
}
