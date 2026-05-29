using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneId.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropUserIsInternalAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_internal_admin",
                table: "users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_internal_admin",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
