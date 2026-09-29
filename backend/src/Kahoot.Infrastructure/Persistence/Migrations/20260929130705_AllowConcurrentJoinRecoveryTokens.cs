using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kahoot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowConcurrentJoinRecoveryTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_participant_session_tokens_participant",
                table: "participant_session_tokens");

            migrationBuilder.DropIndex(
                name: "ux_participant_tokens_host_account_game_participant",
                table: "participant_session_tokens");

            migrationBuilder.CreateIndex(
                name: "ix_participant_session_tokens_participant",
                table: "participant_session_tokens",
                column: "participant_id");

            migrationBuilder.CreateIndex(
                name: "ix_participant_tokens_host_account_game_participant",
                table: "participant_session_tokens",
                columns: new[] { "host_account_id", "game_id", "participant_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_participant_session_tokens_participant",
                table: "participant_session_tokens");

            migrationBuilder.DropIndex(
                name: "ix_participant_tokens_host_account_game_participant",
                table: "participant_session_tokens");

            migrationBuilder.CreateIndex(
                name: "ux_participant_session_tokens_participant",
                table: "participant_session_tokens",
                column: "participant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_participant_tokens_host_account_game_participant",
                table: "participant_session_tokens",
                columns: new[] { "host_account_id", "game_id", "participant_id" },
                unique: true);
        }
    }
}
