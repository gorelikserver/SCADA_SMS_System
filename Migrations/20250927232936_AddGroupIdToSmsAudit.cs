using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SCADASMSSystem.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupIdToSmsAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "date_dimension",
                columns: table => new
                {
                    date_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    full_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    day_of_week = table.Column<byte>(type: "tinyint", nullable: false),
                    day_name = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    day_of_month = table.Column<byte>(type: "tinyint", nullable: false),
                    day_of_year = table.Column<short>(type: "smallint", nullable: false),
                    week_of_year = table.Column<byte>(type: "tinyint", nullable: false),
                    month = table.Column<byte>(type: "tinyint", nullable: false),
                    month_name = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    quarter = table.Column<byte>(type: "tinyint", nullable: false),
                    year = table.Column<short>(type: "smallint", nullable: false),
                    is_weekend = table.Column<bool>(type: "bit", nullable: false),
                    hebrew_date = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    jewish_holiday = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    is_jewish_holiday = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    is_sabbatical_holiday = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_date_dimension", x => x.date_id);
                });

            migrationBuilder.CreateTable(
                name: "groups",
                columns: table => new
                {
                    group_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    group_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_groups", x => x.group_id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    phone_number = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    user_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    sms_enabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    special_days_enabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "group_members",
                columns: table => new
                {
                    group_member_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    group_id = table.Column<int>(type: "int", nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_members", x => x.group_member_id);
                    table.ForeignKey(
                        name: "FK_group_members_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "group_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_group_members_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sms_audit",
                columns: table => new
                {
                    audit_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    alarm_id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    group_id = table.Column<int>(type: "int", nullable: true),
                    phone_number = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    alarm_description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    status = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    message_status = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    api_response = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sms_audit", x => x.audit_id);
                    table.ForeignKey(
                        name: "FK_sms_audit_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "group_id");
                    table.ForeignKey(
                        name: "FK_sms_audit_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_date_dimension_full_date",
                table: "date_dimension",
                column: "full_date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_date_dimension_is_sabbatical_holiday",
                table: "date_dimension",
                column: "is_sabbatical_holiday");

            migrationBuilder.CreateIndex(
                name: "IX_group_members_group_id_user_id",
                table: "group_members",
                columns: new[] { "group_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_group_members_user_id",
                table: "group_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_sms_audit_alarm_id",
                table: "sms_audit",
                column: "alarm_id");

            migrationBuilder.CreateIndex(
                name: "IX_sms_audit_created_at",
                table: "sms_audit",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_sms_audit_group_id",
                table: "sms_audit",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "IX_sms_audit_user_id",
                table: "sms_audit",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_phone_number",
                table: "users",
                column: "phone_number");

            migrationBuilder.CreateIndex(
                name: "IX_users_sms_enabled",
                table: "users",
                column: "sms_enabled");

            migrationBuilder.CreateIndex(
                name: "IX_users_special_days_enabled",
                table: "users",
                column: "special_days_enabled");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "date_dimension");

            migrationBuilder.DropTable(
                name: "group_members");

            migrationBuilder.DropTable(
                name: "sms_audit");

            migrationBuilder.DropTable(
                name: "groups");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
