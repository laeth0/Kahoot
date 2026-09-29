using System;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kahoot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:game_status", "created,finished,leaderboard,lobby,question_active,question_results")
                .Annotation("Npgsql:Enum:user_role", "host,system_admin")
                .Annotation("Npgsql:Enum:user_status", "active,suspended");

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_username = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    normalized_username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    role = table.Column<UserRole>(type: "user_role", nullable: false),
                    status = table.Column<UserStatus>(type: "user_status", nullable: false),
                    token_security_version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    revision = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    termination_pending = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

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

            migrationBuilder.CreateTable(
                name: "quizzes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    host_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_published = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    revision = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quizzes", x => x.id);
                    table.UniqueConstraint("ak_quizzes_id_host_account_id", x => new { x.id, x.host_account_id });
                    table.ForeignKey(
                        name: "fk_quizzes_users_host_account_id",
                        column: x => x.host_account_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    rotated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "games",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    host_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_quiz_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    pin = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    status = table.Column<GameStatus>(type: "game_status", nullable: false),
                    state_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    presence_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    reserved_participant_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    next_seat_number = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    current_question_index = table.Column<int>(type: "integer", nullable: true),
                    host_grace_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_terminated_by_suspension = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_games", x => x.id);
                    table.UniqueConstraint("ak_games_id_host_account_id", x => new { x.id, x.host_account_id });
                    table.ForeignKey(
                        name: "fk_games_quizzes_source_quiz_id_host_account_id",
                        columns: x => new { x.source_quiz_id, x.host_account_id },
                        principalTable: "quizzes",
                        principalColumns: new[] { "id", "host_account_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_games_users_host_account_id",
                        column: x => x.host_account_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    host_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quiz_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    image_id = table.Column<Guid>(type: "uuid", nullable: true),
                    duration_seconds = table.Column<int>(type: "integer", nullable: false),
                    base_points = table.Column<int>(type: "integer", nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_questions", x => x.id);
                    table.UniqueConstraint("ak_questions_id_host_account_id", x => new { x.id, x.host_account_id });
                    table.ForeignKey(
                        name: "fk_questions_question_images_image_id_host_account_id",
                        columns: x => new { x.image_id, x.host_account_id },
                        principalTable: "question_images",
                        principalColumns: new[] { "id", "host_account_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_questions_quizzes_quiz_id_host_account_id",
                        columns: x => new { x.quiz_id, x.host_account_id },
                        principalTable: "quizzes",
                        principalColumns: new[] { "id", "host_account_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "game_command_idempotency",
                columns: table => new
                {
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    command_id = table.Column<Guid>(type: "uuid", nullable: false),
                    host_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    command_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    request_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    result_state_version = table.Column<long>(type: "bigint", nullable: false),
                    response_payload = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_command_idempotency", x => new { x.game_id, x.command_id });
                    table.ForeignKey(
                        name: "fk_game_command_idempotency_games_game_id_host_account_id",
                        columns: x => new { x.game_id, x.host_account_id },
                        principalTable: "games",
                        principalColumns: new[] { "id", "host_account_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "game_question_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    host_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    image_id = table.Column<Guid>(type: "uuid", nullable: true),
                    image_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    duration_seconds = table.Column<int>(type: "integer", nullable: false),
                    base_points = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    initial_eligible_participant_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    effective_eligible_participant_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    accepted_answer_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    results_materialized_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_question_snapshots", x => x.id);
                    table.UniqueConstraint("ak_game_question_snapshots_id_host_account_id", x => new { x.id, x.host_account_id });
                    table.UniqueConstraint("ak_game_question_snapshots_id_host_account_id_game_id", x => new { x.id, x.host_account_id, x.game_id });
                    table.ForeignKey(
                        name: "fk_game_question_snapshots_games_game_id_host_account_id",
                        columns: x => new { x.game_id, x.host_account_id },
                        principalTable: "games",
                        principalColumns: new[] { "id", "host_account_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_game_question_snapshots_question_images_image_id_host_accou",
                        columns: x => new { x.image_id, x.host_account_id },
                        principalTable: "question_images",
                        principalColumns: new[] { "id", "host_account_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "participants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    host_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_nickname = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    normalized_nickname = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    seat_number = table.Column<int>(type: "integer", nullable: false),
                    join_operation_id_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    join_recovery_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_removed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    total_score = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    rank = table.Column<int>(type: "integer", nullable: true),
                    connection_generation = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_participants", x => x.id);
                    table.UniqueConstraint("ak_participants_id_host_account_id_game_id", x => new { x.id, x.host_account_id, x.game_id });
                    table.ForeignKey(
                        name: "fk_participants_games_game_id_host_account_id",
                        columns: x => new { x.game_id, x.host_account_id },
                        principalTable: "games",
                        principalColumns: new[] { "id", "host_account_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "choices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    host_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    is_correct = table.Column<bool>(type: "boolean", nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_choices", x => x.id);
                    table.ForeignKey(
                        name: "fk_choices_questions_question_id_host_account_id",
                        columns: x => new { x.question_id, x.host_account_id },
                        principalTable: "questions",
                        principalColumns: new[] { "id", "host_account_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "game_choice_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    host_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    is_correct = table.Column<bool>(type: "boolean", nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false),
                    selection_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_choice_snapshots", x => x.id);
                    table.UniqueConstraint("ak_game_choice_snapshots_id_host_account_id_game_question_id", x => new { x.id, x.host_account_id, x.game_question_id });
                    table.ForeignKey(
                        name: "fk_game_choice_snapshots_game_question_snapshots_game_question",
                        columns: x => new { x.game_question_id, x.host_account_id },
                        principalTable: "game_question_snapshots",
                        principalColumns: new[] { "id", "host_account_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "answer_submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    host_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    response_time_ms = table.Column<int>(type: "integer", nullable: false),
                    is_correct = table.Column<bool>(type: "boolean", nullable: false),
                    points_awarded = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_answer_submissions", x => x.id);
                    table.UniqueConstraint("ak_answer_submissions_id_host_account_id_game_question_id", x => new { x.id, x.host_account_id, x.game_question_id });
                    table.ForeignKey(
                        name: "fk_answer_submissions_game_question_snapshots_game_question_id",
                        columns: x => new { x.game_question_id, x.host_account_id, x.game_id },
                        principalTable: "game_question_snapshots",
                        principalColumns: new[] { "id", "host_account_id", "game_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_answer_submissions_games_game_id_host_account_id",
                        columns: x => new { x.game_id, x.host_account_id },
                        principalTable: "games",
                        principalColumns: new[] { "id", "host_account_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_answer_submissions_participants_participant_id_host_account",
                        columns: x => new { x.participant_id, x.host_account_id, x.game_id },
                        principalTable: "participants",
                        principalColumns: new[] { "id", "host_account_id", "game_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "participant_session_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    host_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_participant_session_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_participant_session_tokens_participants_participant_id_host",
                        columns: x => new { x.participant_id, x.host_account_id, x.game_id },
                        principalTable: "participants",
                        principalColumns: new[] { "id", "host_account_id", "game_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "answer_submission_choices",
                columns: table => new
                {
                    answer_submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_choice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    host_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_question_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_answer_submission_choices", x => new { x.answer_submission_id, x.game_choice_id });
                    table.ForeignKey(
                        name: "fk_answer_submission_choices_answer_submissions_answer_submiss",
                        columns: x => new { x.answer_submission_id, x.host_account_id, x.game_question_id },
                        principalTable: "answer_submissions",
                        principalColumns: new[] { "id", "host_account_id", "game_question_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_answer_submission_choices_game_choice_snapshots_game_choice",
                        columns: x => new { x.game_choice_id, x.host_account_id, x.game_question_id },
                        principalTable: "game_choice_snapshots",
                        principalColumns: new[] { "id", "host_account_id", "game_question_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_answer_submission_choices_answer_submission_id_host_account",
                table: "answer_submission_choices",
                columns: new[] { "answer_submission_id", "host_account_id", "game_question_id" });

            migrationBuilder.CreateIndex(
                name: "ix_answer_submission_choices_game_choice_id_host_account_id_ga",
                table: "answer_submission_choices",
                columns: new[] { "game_choice_id", "host_account_id", "game_question_id" });

            migrationBuilder.CreateIndex(
                name: "ix_answer_submission_choices_question_choice",
                table: "answer_submission_choices",
                columns: new[] { "game_question_id", "game_choice_id" });

            migrationBuilder.CreateIndex(
                name: "ix_answer_submissions_game_id_host_account_id",
                table: "answer_submissions",
                columns: new[] { "game_id", "host_account_id" });

            migrationBuilder.CreateIndex(
                name: "ix_answer_submissions_game_question_id_host_account_id_game_id",
                table: "answer_submissions",
                columns: new[] { "game_question_id", "host_account_id", "game_id" });

            migrationBuilder.CreateIndex(
                name: "ix_answer_submissions_participant_id_host_account_id_game_id",
                table: "answer_submissions",
                columns: new[] { "participant_id", "host_account_id", "game_id" });

            migrationBuilder.CreateIndex(
                name: "ix_answer_submissions_question_time",
                table: "answer_submissions",
                columns: new[] { "game_question_id", "submitted_at" });

            migrationBuilder.CreateIndex(
                name: "ux_answer_submissions_id_host_account_question",
                table: "answer_submissions",
                columns: new[] { "id", "host_account_id", "game_question_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_answer_submissions_once_per_question",
                table: "answer_submissions",
                columns: new[] { "game_id", "game_question_id", "participant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_choices_host_account_question_order",
                table: "choices",
                columns: new[] { "host_account_id", "question_id", "order_index" });

            migrationBuilder.CreateIndex(
                name: "ix_choices_question_id_host_account_id",
                table: "choices",
                columns: new[] { "question_id", "host_account_id" });

            migrationBuilder.CreateIndex(
                name: "ux_choices_id_host_account",
                table: "choices",
                columns: new[] { "id", "host_account_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_choices_question_order",
                table: "choices",
                columns: new[] { "question_id", "order_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_game_choice_snapshots_game_question_id_host_account_id",
                table: "game_choice_snapshots",
                columns: new[] { "game_question_id", "host_account_id" });

            migrationBuilder.CreateIndex(
                name: "ux_game_choices_id_host_account",
                table: "game_choice_snapshots",
                columns: new[] { "id", "host_account_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_game_choices_id_host_account_question",
                table: "game_choice_snapshots",
                columns: new[] { "id", "host_account_id", "game_question_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_game_choices_question_order",
                table: "game_choice_snapshots",
                columns: new[] { "game_question_id", "order_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_game_command_idempotency_game_id_host_account_id",
                table: "game_command_idempotency",
                columns: new[] { "game_id", "host_account_id" });

            migrationBuilder.CreateIndex(
                name: "ix_game_command_idempotency_host_account_game_time",
                table: "game_command_idempotency",
                columns: new[] { "host_account_id", "game_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_game_question_snapshots_game_id_host_account_id",
                table: "game_question_snapshots",
                columns: new[] { "game_id", "host_account_id" });

            migrationBuilder.CreateIndex(
                name: "ix_game_question_snapshots_image_id_host_account_id",
                table: "game_question_snapshots",
                columns: new[] { "image_id", "host_account_id" });

            migrationBuilder.CreateIndex(
                name: "ix_game_questions_host_account_game_order",
                table: "game_question_snapshots",
                columns: new[] { "host_account_id", "game_id", "order_index" });

            migrationBuilder.CreateIndex(
                name: "ux_game_questions_game_order",
                table: "game_question_snapshots",
                columns: new[] { "game_id", "order_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_game_questions_id_host_account",
                table: "game_question_snapshots",
                columns: new[] { "id", "host_account_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_game_questions_id_host_account_game",
                table: "game_question_snapshots",
                columns: new[] { "id", "host_account_id", "game_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_games_abandonment_sweep",
                table: "games",
                columns: new[] { "status", "host_grace_expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_games_host_account_created_id",
                table: "games",
                columns: new[] { "host_account_id", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_games_host_account_source_quiz",
                table: "games",
                columns: new[] { "host_account_id", "source_quiz_id" });

            migrationBuilder.CreateIndex(
                name: "ix_games_host_account_status",
                table: "games",
                columns: new[] { "host_account_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_games_source_quiz_id_host_account_id",
                table: "games",
                columns: new[] { "source_quiz_id", "host_account_id" });

            migrationBuilder.CreateIndex(
                name: "ux_games_active_pin",
                table: "games",
                column: "pin",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_games_id_host_account",
                table: "games",
                columns: new[] { "id", "host_account_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_participant_session_tokens_expires_at",
                table: "participant_session_tokens",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_participant_session_tokens_participant_id_host_account_id_g",
                table: "participant_session_tokens",
                columns: new[] { "participant_id", "host_account_id", "game_id" });

            migrationBuilder.CreateIndex(
                name: "ux_participant_session_tokens_participant",
                table: "participant_session_tokens",
                column: "participant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_participant_session_tokens_token_hash",
                table: "participant_session_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_participant_tokens_host_account_game_participant",
                table: "participant_session_tokens",
                columns: new[] { "host_account_id", "game_id", "participant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_participants_game_id_host_account_id",
                table: "participants",
                columns: new[] { "game_id", "host_account_id" });

            migrationBuilder.CreateIndex(
                name: "ix_participants_host_account_game_removed",
                table: "participants",
                columns: new[] { "host_account_id", "game_id", "is_removed" });

            migrationBuilder.CreateIndex(
                name: "ux_participants_game_join_operation",
                table: "participants",
                columns: new[] { "game_id", "join_operation_id_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_participants_game_nickname",
                table: "participants",
                columns: new[] { "game_id", "normalized_nickname" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_participants_game_seat",
                table: "participants",
                columns: new[] { "game_id", "seat_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_participants_id_host_account_game",
                table: "participants",
                columns: new[] { "id", "host_account_id", "game_id" },
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

            migrationBuilder.CreateIndex(
                name: "ix_questions_host_account_quiz_order",
                table: "questions",
                columns: new[] { "host_account_id", "quiz_id", "order_index" });

            migrationBuilder.CreateIndex(
                name: "ix_questions_image_id_host_account_id",
                table: "questions",
                columns: new[] { "image_id", "host_account_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_questions_quiz_id_host_account_id",
                table: "questions",
                columns: new[] { "quiz_id", "host_account_id" });

            migrationBuilder.CreateIndex(
                name: "ux_questions_id_host_account",
                table: "questions",
                columns: new[] { "id", "host_account_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_questions_quiz_order",
                table: "questions",
                columns: new[] { "quiz_id", "order_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_quizzes_host_account_created_id",
                table: "quizzes",
                columns: new[] { "host_account_id", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ux_quizzes_id_host_account",
                table: "quizzes",
                columns: new[] { "id", "host_account_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_expires_at",
                table: "refresh_tokens",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_revoked_at",
                table: "refresh_tokens",
                column: "revoked_at");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_family",
                table: "refresh_tokens",
                columns: new[] { "user_id", "token_family_id" });

            migrationBuilder.CreateIndex(
                name: "ux_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_admin_listing",
                table: "users",
                columns: new[] { "role", "status", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ux_users_normalized_username",
                table: "users",
                column: "normalized_username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "answer_submission_choices");

            migrationBuilder.DropTable(
                name: "choices");

            migrationBuilder.DropTable(
                name: "game_command_idempotency");

            migrationBuilder.DropTable(
                name: "participant_session_tokens");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "answer_submissions");

            migrationBuilder.DropTable(
                name: "game_choice_snapshots");

            migrationBuilder.DropTable(
                name: "questions");

            migrationBuilder.DropTable(
                name: "participants");

            migrationBuilder.DropTable(
                name: "game_question_snapshots");

            migrationBuilder.DropTable(
                name: "games");

            migrationBuilder.DropTable(
                name: "question_images");

            migrationBuilder.DropTable(
                name: "quizzes");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
