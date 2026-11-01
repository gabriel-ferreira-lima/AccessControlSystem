#!/bin/bash
# Inicialização do PostgreSQL de DESENVOLVIMENTO (roda só na primeira subida, com o volume vazio).
# Cria os papéis e os três bancos fixos. Contrato: specs/.../contracts/database-isolation.md.
#
#   acs_migrator  dono dos bancos e das tabelas; aplica migrations (usado só pelo Migrator)
#   acs_app       usado pela Api; sem DDL, sem DELETE, sem BYPASSRLS
#
# Bancos:  acs_catalog (catálogo), acs_shared_01 (base compartilhada) e acs_dedicated_01 (base dedicada).
# As migrations NÃO são aplicadas aqui: quem as aplica é o Migrator (nunca o dotnet ef database update).
#
# Este script também funciona se o entrypoint do Postgres o executar com "source" (arquivo sem
# permissão de execução), por isso não altera as opções do shell.

: "${ACS_MIGRATOR_PASSWORD:?defina ACS_MIGRATOR_PASSWORD no .env}"
: "${ACS_APP_PASSWORD:?defina ACS_APP_PASSWORD no .env}"

ADMIN_USER="${POSTGRES_USER:-postgres}"

# Executa um comando SQL como superusuário, parando no primeiro erro.
admin_psql() {
    psql -v ON_ERROR_STOP=1 --username "$ADMIN_USER" "$@"
}

# 1. Papéis (sem senha) e senhas. A senha vai por variável do psql, nunca colada no SQL.
admin_psql --dbname postgres --file /docker/sql/roles.sql || exit 1

admin_psql --dbname postgres \
    --set migrator_password="$ACS_MIGRATOR_PASSWORD" \
    --set app_password="$ACS_APP_PASSWORD" <<'SQL' || exit 1
ALTER ROLE acs_migrator PASSWORD :'migrator_password';
ALTER ROLE acs_app PASSWORD :'app_password';
SQL

# 2. Bancos, com dono acs_migrator.
for database in acs_catalog acs_shared_01 acs_dedicated_01; do
    admin_psql --dbname postgres --command "CREATE DATABASE ${database} OWNER acs_migrator" || exit 1
done

# 3. Privilégios padrão do papel da aplicação, por banco (precisam rodar conectados ao banco certo).
admin_psql --dbname acs_catalog --file /docker/sql/catalog-db-grants.sql || exit 1
admin_psql --dbname acs_shared_01 --file /docker/sql/tenant-db-grants.sql || exit 1
admin_psql --dbname acs_dedicated_01 --file /docker/sql/tenant-db-grants.sql || exit 1

echo "Papéis e bancos de desenvolvimento criados (acs_catalog, acs_shared_01, acs_dedicated_01)."
