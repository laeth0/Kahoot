using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kahoot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAuditColumnsAddMediaAndPublish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "created_at",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "quizzes");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "quizzes");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "hosts");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "hosts");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "game_sessions");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "game_sessions");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "choices");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "choices");

            migrationBuilder.AddColumn<bool>(
                name: "is_published",
                table: "quizzes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "image_url",
                table: "questions",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "text",
                table: "choices",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300);

            migrationBuilder.AddColumn<string>(
                name: "image_url",
                table: "choices",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_published",
                table: "quizzes");

            migrationBuilder.DropColumn(
                name: "image_url",
                table: "questions");

            migrationBuilder.DropColumn(
                name: "image_url",
                table: "choices");

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "refresh_tokens",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "quizzes",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "quizzes",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "questions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "questions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "participants",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "participants",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "hosts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "hosts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "game_sessions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "game_sessions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AlterColumn<string>(
                name: "text",
                table: "choices",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "choices",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "choices",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");
        }
    }
}
