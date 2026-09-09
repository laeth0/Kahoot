using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kahoot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hosts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hosts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "quizzes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    host_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quizzes", x => x.id);
                    table.ForeignKey(
                        name: "fk_quizzes_hosts_host_id",
                        column: x => x.host_id,
                        principalTable: "hosts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    host_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    replaced_by_token_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_hosts_host_id",
                        column: x => x.host_id,
                        principalTable: "hosts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_refresh_tokens_replaced_by_token_id",
                        column: x => x.replaced_by_token_id,
                        principalTable: "refresh_tokens",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quiz_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    time_limit_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 20),
                    points = table.Column<int>(type: "integer", nullable: false, defaultValue: 1000),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_questions", x => x.id);
                    table.CheckConstraint("ck_question_points", "points >= 0");
                    table.CheckConstraint("ck_question_time_limit_seconds", "time_limit_seconds >= 5 AND time_limit_seconds <= 300");
                    table.ForeignKey(
                        name: "fk_questions_quizzes_quiz_id",
                        column: x => x.quiz_id,
                        principalTable: "quizzes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "choices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    is_correct = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_choices", x => x.id);
                    table.ForeignKey(
                        name: "fk_choices_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "game_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quiz_id = table.Column<Guid>(type: "uuid", nullable: false),
                    host_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pin = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'Created'"),
                    current_question_id = table.Column<Guid>(type: "uuid", nullable: true),
                    current_question_index = table.Column<int>(type: "integer", nullable: true),
                    current_question_started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    current_question_ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    finished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_sessions", x => x.id);
                    table.CheckConstraint("ck_game_session_question_window", "current_question_ends_at IS NULL OR current_question_started_at IS NULL OR current_question_ends_at >= current_question_started_at");
                    table.ForeignKey(
                        name: "fk_game_sessions_hosts_host_id",
                        column: x => x.host_id,
                        principalTable: "hosts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_game_sessions_questions_current_question_id",
                        column: x => x.current_question_id,
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_game_sessions_quizzes_quiz_id",
                        column: x => x.quiz_id,
                        principalTable: "quizzes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "participants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nickname = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    nickname_normalized = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    session_token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    connection_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    total_score = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_rank = table.Column<int>(type: "integer", nullable: true),
                    is_removed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    removed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_participants", x => x.id);
                    table.CheckConstraint("ck_participant_total_score", "total_score >= 0");
                    table.ForeignKey(
                        name: "fk_participants_game_sessions_game_session_id",
                        column: x => x.game_session_id,
                        principalTable: "game_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "answers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    selected_choice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_correct = table.Column<bool>(type: "boolean", nullable: false),
                    points_awarded = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    response_time_ms = table.Column<int>(type: "integer", nullable: false),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_answers", x => x.id);
                    table.CheckConstraint("ck_answer_points_awarded", "points_awarded >= 0");
                    table.CheckConstraint("ck_answer_response_time_ms", "response_time_ms >= 0");
                    table.ForeignKey(
                        name: "fk_answers_choices_selected_choice_id",
                        column: x => x.selected_choice_id,
                        principalTable: "choices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_answers_game_sessions_game_session_id",
                        column: x => x.game_session_id,
                        principalTable: "game_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_answers_participants_participant_id",
                        column: x => x.participant_id,
                        principalTable: "participants",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_answers_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_answer_game_question",
                table: "answers",
                columns: new[] { "game_session_id", "question_id" });

            migrationBuilder.CreateIndex(
                name: "ix_answer_participant_id",
                table: "answers",
                column: "participant_id");

            migrationBuilder.CreateIndex(
                name: "ix_answer_selected_choice_id",
                table: "answers",
                column: "selected_choice_id");

            migrationBuilder.CreateIndex(
                name: "ix_answers_question_id",
                table: "answers",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "uq_answer_participant_question",
                table: "answers",
                columns: new[] { "game_session_id", "question_id", "participant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_choice_question_id",
                table: "choices",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "uq_choice_question_order",
                table: "choices",
                columns: new[] { "question_id", "order_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_game_session_current_question_id",
                table: "game_sessions",
                column: "current_question_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_session_host_id",
                table: "game_sessions",
                column: "host_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_session_quiz_id",
                table: "game_sessions",
                column: "quiz_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_session_status",
                table: "game_sessions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "uq_game_session_active_pin",
                table: "game_sessions",
                column: "pin",
                unique: true,
                filter: "status <> 'Finished'");

            migrationBuilder.CreateIndex(
                name: "uq_host_username",
                table: "hosts",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_participant_connection_id",
                table: "participants",
                column: "connection_id");

            migrationBuilder.CreateIndex(
                name: "ix_participant_game_score",
                table: "participants",
                columns: new[] { "game_session_id", "total_score" });

            migrationBuilder.CreateIndex(
                name: "ix_participant_game_session_id",
                table: "participants",
                column: "game_session_id");

            migrationBuilder.CreateIndex(
                name: "uq_participant_game_nickname",
                table: "participants",
                columns: new[] { "game_session_id", "nickname_normalized" },
                unique: true,
                filter: "is_removed = false");

            migrationBuilder.CreateIndex(
                name: "uq_participant_session_token",
                table: "participants",
                column: "session_token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_question_quiz_id",
                table: "questions",
                column: "quiz_id");

            migrationBuilder.CreateIndex(
                name: "uq_question_quiz_order",
                table: "questions",
                columns: new[] { "quiz_id", "order_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_quiz_host_id",
                table: "quizzes",
                column: "host_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_token_host_id",
                table: "refresh_tokens",
                column: "host_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_replaced_by_token_id",
                table: "refresh_tokens",
                column: "replaced_by_token_id");

            migrationBuilder.CreateIndex(
                name: "uq_refresh_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "answers");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "choices");

            migrationBuilder.DropTable(
                name: "participants");

            migrationBuilder.DropTable(
                name: "game_sessions");

            migrationBuilder.DropTable(
                name: "questions");

            migrationBuilder.DropTable(
                name: "quizzes");

            migrationBuilder.DropTable(
                name: "hosts");
        }
    }
}
