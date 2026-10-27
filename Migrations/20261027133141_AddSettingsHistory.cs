using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SCADASMSSystem.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddSettingsHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "scada_provider_profile_versions",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    profile_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    profile_kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    profile_hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    profile_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    activated_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    activated_by = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    verified_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    verified_by = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scada_provider_profile_versions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "scada_settings_changes",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    changed_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    changed_by = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    area = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scada_settings_changes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_scada_provider_profile_versions_profile_hash",
                table: "scada_provider_profile_versions",
                column: "profile_hash");

            migrationBuilder.CreateIndex(
                name: "IX_scada_settings_changes_changed_at",
                table: "scada_settings_changes",
                column: "changed_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "scada_provider_profile_versions");

            migrationBuilder.DropTable(
                name: "scada_settings_changes");
        }
    }
}
