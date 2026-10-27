using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SCADASMSSystem.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddCriticalSmsDeliveries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "scada_critical_sms_deliveries",
                columns: table => new
                {
                    delivery_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    alarm_id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    group_id = table.Column<int>(type: "int", nullable: false),
                    phone_number = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    provider_status_code = table.Column<int>(type: "int", nullable: false),
                    attempts = table.Column<int>(type: "int", nullable: false),
                    provider_message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scada_critical_sms_deliveries", x => x.delivery_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_scada_critical_sms_deliveries_alarm_id_phone_number",
                table: "scada_critical_sms_deliveries",
                columns: new[] { "alarm_id", "phone_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_scada_critical_sms_deliveries_status",
                table: "scada_critical_sms_deliveries",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "scada_critical_sms_deliveries");
        }
    }
}
