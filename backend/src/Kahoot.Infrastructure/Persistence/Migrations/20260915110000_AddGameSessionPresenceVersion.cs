using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kahoot.Infrastructure.Persistence.Migrations
{
    public partial class AddGameSessionPresenceVersion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "presence_version",
                table: "game_sessions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "presence_version",
                table: "game_sessions");
        }
    }
}
