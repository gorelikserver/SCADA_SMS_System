using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SCADASMSSystem.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddAlarmAcknowledgements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "scada_alarm_acknowledgements",
                columns: table => new
                {
                    alarm_id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    acknowledged_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    acknowledged_by = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scada_alarm_acknowledgements", x => x.alarm_id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "scada_alarm_acknowledgements");
        }
    }
}
