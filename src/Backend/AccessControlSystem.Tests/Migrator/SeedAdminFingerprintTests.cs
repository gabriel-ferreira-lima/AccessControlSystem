using AccessControlSystem.Migrator.Orchestration;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AccessControlSystem.Tests.Migrator;

// FR-031/FR-035: a verificação do administrador fixo lê o id e o e-mail DA PRÓPRIA migration SeedAdmin, sem duplicá-los
// à mão. Os literais abaixo são escritos aqui, independentes do código de produção: se a migration mudar por acidente,
// o teste acusa (e se mudar de forma, a extração FALHA ALTO em vez de devolver um valor vazio que aprovaria tudo).
public class SeedAdminFingerprintTests
{
    [Fact]
    public void Current_le_a_identidade_fixa_da_migration_SeedAdmin()
    {
        var fingerprint = SeedAdminFingerprint.Current;

        Assert.Equal("operators", fingerprint.Table);
        Assert.Equal("id", fingerprint.IdColumn);
        Assert.Equal("email", fingerprint.EmailColumn);
        Assert.Equal(Guid.Parse("cc59e349-efbd-4800-9726-f38a325e4d3d"), fingerprint.Id);
        Assert.Equal("admin@admin.com.br", fingerprint.Email);
    }

    [Theory]
    [InlineData("admin@admin.com.br", true)]
    [InlineData("ADMIN@Admin.com.BR", true)]
    [InlineData("Admin@admin.COM.br", true)]
    [InlineData("outro@admin.com.br", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void MatchesEmail_compara_sem_diferenciar_maiusculas_de_minusculas(string? candidate, bool expected)
    {
        Assert.Equal(expected, SeedAdminFingerprint.Current.MatchesEmail(candidate));
    }

    [Fact]
    public void From_le_os_valores_de_uma_migration_qualquer()
    {
        var id = Guid.NewGuid();
        var fingerprint = SeedAdminFingerprint.From(new MigrationComInsert("pessoas", ["nome", "id", "email"], [["x", id, "a@b.test"]]));

        Assert.Equal("pessoas", fingerprint.Table);
        Assert.Equal("id", fingerprint.IdColumn);
        Assert.Equal("email", fingerprint.EmailColumn);
        Assert.Equal(id, fingerprint.Id);   // acha a coluna pela posição, não por ordem fixa
        Assert.Equal("a@b.test", fingerprint.Email);
    }

    [Fact]
    public void From_sem_InsertDataOperation_falha_alto()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => SeedAdminFingerprint.From(new MigrationSemInsert()));

        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
    }

    [Fact]
    public void From_com_InsertDataOperation_sem_a_coluna_email_falha_alto()
    {
        var migration = new MigrationComInsert("operators", ["id", "role"], [[Guid.NewGuid(), 1]]);

        var exception = Assert.Throws<InvalidOperationException>(() => SeedAdminFingerprint.From(migration));

        Assert.Contains("email", exception.Message);
    }

    [Fact]
    public void From_com_InsertDataOperation_sem_a_coluna_id_falha_alto()
    {
        var migration = new MigrationComInsert("operators", ["email", "role"], [["a@b.test", 1]]);

        var exception = Assert.Throws<InvalidOperationException>(() => SeedAdminFingerprint.From(migration));

        Assert.Contains("id", exception.Message);
    }

    [Theory]
    [InlineData("id_nao_e_guid")]
    [InlineData("email_vazio")]
    [InlineData("duas_linhas")]
    public void From_com_valores_inesperados_falha_alto(string problem)
    {
        var id = Guid.NewGuid();
        Migration migration = problem switch
        {
            "id_nao_e_guid" => new MigrationComInsert("operators", ["id", "email"], [["nao-e-guid", "a@b.test"]]),
            "email_vazio" => new MigrationComInsert("operators", ["id", "email"], [[id, ""]]),
            _ => new MigrationComInsert("operators", ["id", "email"], [[id, "a@b.test"], [Guid.NewGuid(), "c@d.test"]]),
        };

        Assert.Throws<InvalidOperationException>(() => SeedAdminFingerprint.From(migration));
    }

    /// <summary>Migration falsa sem nenhum <c>InsertData</c>.</summary>
    private sealed class MigrationSemInsert : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql("select 1");
    }

    /// <summary>Migration falsa com um <c>InsertData</c> de formato controlado pelo teste.</summary>
    private sealed class MigrationComInsert(string table, string[] columns, object[][] rows) : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var values = new object[rows.Length, columns.Length];
            for (var row = 0; row < rows.Length; row++)
                for (var column = 0; column < columns.Length; column++)
                    values[row, column] = rows[row][column];

            migrationBuilder.InsertData(table: table, columns: columns, values: values);
        }
    }
}
