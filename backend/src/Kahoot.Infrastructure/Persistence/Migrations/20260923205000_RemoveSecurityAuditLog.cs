using Kahoot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Kahoot.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260923205000_RemoveSecurityAuditLog")]
public partial class RemoveSecurityAuditLog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS security_audit_logs;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Earlier EF migrations never created this table, so there is no schema to restore.
    }
}
