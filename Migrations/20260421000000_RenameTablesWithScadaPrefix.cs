using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SCADASMSSystem.Web.Migrations
{
    /// <inheritdoc />
    public partial class RenameTablesWithScadaPrefix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "users",
                newName: "scada_users");

            migrationBuilder.RenameTable(
                name: "groups",
                newName: "scada_groups");

            migrationBuilder.RenameTable(
                name: "group_members",
                newName: "scada_group_members");

            migrationBuilder.RenameTable(
                name: "sms_audit",
                newName: "scada_sms_audit");

            migrationBuilder.RenameTable(
                name: "date_dimension",
                newName: "scada_date_dimension");

            migrationBuilder.RenameTable(
                name: "alarm_action_audit",
                newName: "scada_alarm_action_audit");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "scada_users",
                newName: "users");

            migrationBuilder.RenameTable(
                name: "scada_groups",
                newName: "groups");

            migrationBuilder.RenameTable(
                name: "scada_group_members",
                newName: "group_members");

            migrationBuilder.RenameTable(
                name: "scada_sms_audit",
                newName: "sms_audit");

            migrationBuilder.RenameTable(
                name: "scada_date_dimension",
                newName: "date_dimension");

            migrationBuilder.RenameTable(
                name: "scada_alarm_action_audit",
                newName: "alarm_action_audit");
        }
    }
}
