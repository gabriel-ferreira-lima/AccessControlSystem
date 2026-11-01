-- Papéis do sistema (nível do cluster PostgreSQL). Contrato: specs/.../contracts/database-isolation.md.
--
-- Os papéis são criados SEM senha: quem executa este script (o 01-init.sh do compose, a fixture dos
-- testes ou o provisionamento) define a senha com ALTER ROLE ... PASSWORD.
--
-- acs_migrator: dono dos bancos e das tabelas; aplica migrations (DDL). BYPASSRLS existe só para
--               migrations de dados futuras que precisem enxergar todos os tenants.
-- acs_app:      usado pela Api. NÃO é dono de nada, NÃO tem BYPASSRLS, NÃO tem DDL.
--
-- Idempotente: pode rodar mais de uma vez.
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'acs_migrator') THEN
        CREATE ROLE acs_migrator LOGIN BYPASSRLS NOSUPERUSER NOCREATEDB NOCREATEROLE;
    END IF;

    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'acs_app') THEN
        CREATE ROLE acs_app LOGIN NOBYPASSRLS NOSUPERUSER NOCREATEDB NOCREATEROLE;
    END IF;
END
$$;
