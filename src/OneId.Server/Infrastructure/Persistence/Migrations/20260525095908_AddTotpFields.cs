using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneId.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTotpFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_totp_enrolled",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "totp_last_used_time_step",
                table: "users",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "totp_secret",
                table: "users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_totp_enrolled",
                table: "users");

            migrationBuilder.DropColumn(
                name: "totp_last_used_time_step",
                table: "users");

            migrationBuilder.DropColumn(
                name: "totp_secret",
                table: "users");
        }
    }
}
