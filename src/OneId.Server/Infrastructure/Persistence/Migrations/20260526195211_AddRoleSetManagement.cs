using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneId.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleSetManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "role_sets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_sets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "group_role_sets",
                columns: table => new
                {
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_set_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_group_role_sets", x => new { x.group_id, x.role_set_id });
                    table.ForeignKey(
                        name: "fk_group_role_sets_role_sets_role_set_id",
                        column: x => x.role_set_id,
                        principalTable: "role_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "role_set_roles",
                columns: table => new
                {
                    role_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_set_roles", x => new { x.role_set_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_role_set_roles_role_sets_role_set_id",
                        column: x => x.role_set_id,
                        principalTable: "role_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_role_set_roles_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_group_role_sets_role_set_id",
                table: "group_role_sets",
                column: "role_set_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_set_roles_role_id",
                table: "role_set_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_sets_tenant_id_name",
                table: "role_sets",
                columns: new[] { "tenant_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "group_role_sets");

            migrationBuilder.DropTable(
                name: "role_set_roles");

            migrationBuilder.DropTable(
                name: "role_sets");
        }
    }
}
