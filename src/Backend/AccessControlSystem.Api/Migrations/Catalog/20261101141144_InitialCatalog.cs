using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccessControlSystem.Api.Migrations.Catalog
{
    /// <inheritdoc />
    public partial class InitialCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_databases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "varchar(100)", nullable: false),
                    kind = table.Column<short>(type: "smallint", nullable: false),
                    schema_version = table.Column<string>(type: "varchar(150)", nullable: true),
                    migration_status = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    migration_attempted_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    migration_error = table.Column<string>(type: "varchar(1000)", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_databases", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cnpj = table.Column<string>(type: "varchar(18)", nullable: false),
                    name = table.Column<string>(type: "varchar(200)", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    database_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.id);
                    table.ForeignKey(
                        name: "FK_tenants_tenant_databases_database_id",
                        column: x => x.database_id,
                        principalTable: "tenant_databases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_databases_migration_status_id",
                table: "tenant_databases",
                columns: new[] { "migration_status", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_databases_name",
                table: "tenant_databases",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenants_cnpj",
                table: "tenants",
                column: "cnpj",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenants_database_id",
                table: "tenants",
                column: "database_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenants_id_active",
                table: "tenants",
                column: "id",
                filter: "is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenants");

            migrationBuilder.DropTable(
                name: "tenant_databases");
        }
    }
}
