-- Privilégios do papel da aplicação em uma BASE DE TENANT (compartilhada ou dedicada).
-- Executar conectado à base, como superusuário ou como acs_migrator.
--
-- As migrations NÃO citam papéis: os privilégios vêm destes privilégios padrão, que valem para toda
-- tabela que o acs_migrator criar neste banco a partir de agora.
-- Sem DELETE: não existe exclusão física de entidade de negócio (Princípio IV da constituição).
GRANT USAGE ON SCHEMA public TO acs_app;

ALTER DEFAULT PRIVILEGES FOR ROLE acs_migrator IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE ON TABLES TO acs_app;
