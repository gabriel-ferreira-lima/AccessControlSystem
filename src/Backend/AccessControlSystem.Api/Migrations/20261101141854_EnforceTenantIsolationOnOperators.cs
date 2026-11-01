using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccessControlSystem.Api.Migrations
{
    /// <inheritdoc />
    public partial class EnforceTenantIsolationOnOperators : Migration
    {
        // Política de isolamento por tenant (contracts/database-isolation.md). NULLIF evita erro de
        // conversão quando a configuração existe mas está vazia (comportamento do PostgreSQL fora da
        // transação em que foi definida); sem tenant informado a comparação é NULL e nenhuma linha passa.
        private const string TenantMatch =
            "tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CONTRAIR: o tenant passa a ser obrigatório. SEM defaultValue de propósito: linhas legadas
            // sem tenant (base adotada sem o tenant inicial configurado) fazem esta migration FALHAR
            // aqui, em vez de serem atribuídas a um tenant adivinhado. A transação desfaz tudo.
            migrationBuilder.AlterColumn<Guid>(
                name: "tenant_id",
                table: "operators",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            // O e-mail deixa de ser único no sistema todo e passa a ser único DENTRO do tenant.
            migrationBuilder.DropIndex(
                name: "IX_operators_email",
                table: "operators");

            migrationBuilder.CreateIndex(
                name: "ix_operators_tenant_id_email",
                table: "operators",
                columns: new[] { "tenant_id", "email" },
                unique: true);

            // Busca por id, lista paginada e contagens do tenant (todo índice começa por tenant_id).
            migrationBuilder.CreateIndex(
                name: "ix_operators_tenant_id_id",
                table: "operators",
                columns: new[] { "tenant_id", "id" });

            // Segunda camada do isolamento, dentro do banco: vale mesmo que o código esqueça o filtro
            // do EF. FORCE faz o RLS valer também para o dono da tabela. Nenhum papel é citado aqui:
            // os privilégios vêm dos privilégios padrão de docker/postgres/sql.
            migrationBuilder.Sql("ALTER TABLE operators ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE operators FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(
                $"CREATE POLICY operators_tenant_isolation ON operators USING ({TenantMatch}) WITH CHECK ({TenantMatch});");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY IF EXISTS operators_tenant_isolation ON operators;");
            migrationBuilder.Sql("ALTER TABLE operators NO FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE operators DISABLE ROW LEVEL SECURITY;");

            migrationBuilder.DropIndex(
                name: "ix_operators_tenant_id_id",
                table: "operators");

            migrationBuilder.DropIndex(
                name: "ix_operators_tenant_id_email",
                table: "operators");

            // Recriar o índice único global falha se houver o mesmo e-mail em tenants diferentes
            // (esperado: voltar atrás exige resolver os duplicados antes).
            migrationBuilder.CreateIndex(
                name: "IX_operators_email",
                table: "operators",
                column: "email",
                unique: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "tenant_id",
                table: "operators",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }
    }
}
