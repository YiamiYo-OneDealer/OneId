using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneId.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDimensionValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dimension_values",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    axis = table.Column<int>(type: "integer", nullable: false),
                    value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dimension_values", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_dimension_values_tenant_id_axis_value",
                table: "dimension_values",
                columns: new[] { "tenant_id", "axis", "value" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dimension_values");
        }
    }
}
