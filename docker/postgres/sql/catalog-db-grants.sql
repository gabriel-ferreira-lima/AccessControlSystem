-- Privilégios do papel da aplicação no BANCO DO CATÁLOGO.
-- Executar conectado ao banco do catálogo, como superusuário ou como acs_migrator.
--
-- A Api só LÊ o catálogo (ITenantCatalog). Quem escreve é o Migrator (acs_migrator) e, no futuro,
-- o sistema de provisionamento.
GRANT USAGE ON SCHEMA public TO acs_app;

ALTER DEFAULT PRIVILEGES FOR ROLE acs_migrator IN SCHEMA public
    GRANT SELECT ON TABLES TO acs_app;
