using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccessControlSystem.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIdToOperators : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // EXPANDIR: a coluna nasce opcional. A migration seguinte (EnforceTenantIsolationOnOperators)
            // a torna obrigatória e liga o RLS.
            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "operators",
                type: "uuid",
                nullable: true);

            // Backfill: as linhas legadas (de antes do multi-tenant) passam a pertencer ao tenant
            // inicial, informado pelo Migrator na MESMA conexão (acs.initial_tenant_id). Sem a
            // configuração e com linhas legadas, elas ficam NULL e a migration seguinte falha no
            // NOT NULL com erro claro: nunca atribui linhas a um tenant adivinhado. Tabela vazia
            // (base nova) passa direto.
            migrationBuilder.Sql(
                """
                UPDATE operators
                   SET tenant_id = NULLIF(current_setting('acs.initial_tenant_id', true), '')::uuid
                 WHERE tenant_id IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "operators");
        }
    }
}
