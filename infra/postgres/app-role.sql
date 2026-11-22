-- Run by the db-init service before every start of sentium-web, as the postgres superuser. Idempotent.
--
-- sentium-web connects as sentium_app: it owns the module schemas (so its migrations can change them) but is not a
-- superuser, so a compromised web process cannot read other databases, run programs through COPY ... PROGRAM or change
-- roles. Only the backup sidecar keeps superuser access.
--
-- Psql variable: app_password.

\set ON_ERROR_STOP on

SELECT 'CREATE ROLE sentium_app LOGIN'
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'sentium_app')
\gexec

ALTER ROLE sentium_app WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD :'app_password';

GRANT CONNECT, CREATE, TEMPORARY ON DATABASE sentiumdb TO sentium_app;

-- Creating an extension needs a superuser; once it exists, the migrations' CREATE EXTENSION IF NOT EXISTS is a no-op.
CREATE EXTENSION IF NOT EXISTS vector;

-- Hands every Sentium schema, and everything in it, to sentium_app: on an install that predates this role, and after a
-- restore (pg_restore --no-owner leaves the restoring superuser as owner).
DO $$
DECLARE
    app oid := (SELECT oid FROM pg_roles WHERE rolname = 'sentium_app');
    item record;
BEGIN
    FOR item IN
        SELECT n.oid, n.nspname
        FROM pg_namespace n
        WHERE n.nspowner <> app
          AND n.nspname NOT IN ('public', 'information_schema')
          AND n.nspname NOT LIKE 'pg\_%'
    LOOP
        EXECUTE format('ALTER SCHEMA %I OWNER TO sentium_app', item.nspname);
    END LOOP;

    -- Tables, views and materialized views; a table's owned sequences follow it.
    FOR item IN
        SELECT c.oid::regclass AS name, c.relkind
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspowner = app
          AND c.relowner <> app
          AND c.relkind IN ('r', 'p', 'v', 'm', 'f')
    LOOP
        EXECUTE format(
            CASE item.relkind WHEN 'v' THEN 'ALTER VIEW %s OWNER TO sentium_app'
                              WHEN 'm' THEN 'ALTER MATERIALIZED VIEW %s OWNER TO sentium_app'
                              WHEN 'f' THEN 'ALTER FOREIGN TABLE %s OWNER TO sentium_app'
                              ELSE 'ALTER TABLE %s OWNER TO sentium_app' END,
            item.name);
    END LOOP;

    -- Sequences not owned by a table column.
    FOR item IN
        SELECT c.oid::regclass AS name
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspowner = app
          AND c.relowner <> app
          AND c.relkind = 'S'
          AND NOT EXISTS (
              SELECT FROM pg_depend d
              WHERE d.classid = 'pg_class'::regclass AND d.objid = c.oid AND d.deptype IN ('a', 'i'))
    LOOP
        EXECUTE format('ALTER SEQUENCE %s OWNER TO sentium_app', item.name);
    END LOOP;

    -- Functions and procedures.
    FOR item IN
        SELECT p.oid::regprocedure AS name, p.prokind
        FROM pg_proc p
        JOIN pg_namespace n ON n.oid = p.pronamespace
        WHERE n.nspowner = app
          AND p.proowner <> app
    LOOP
        EXECUTE format(
            CASE item.prokind WHEN 'p' THEN 'ALTER PROCEDURE %s OWNER TO sentium_app' ELSE 'ALTER ROUTINE %s OWNER TO sentium_app' END,
            item.name);
    END LOOP;

    -- Standalone types (enums, domains, composites not backing a table).
    FOR item IN
        SELECT t.oid::regtype AS name, t.typtype
        FROM pg_type t
        JOIN pg_namespace n ON n.oid = t.typnamespace
        WHERE n.nspowner = app
          AND t.typowner <> app
          AND t.typtype IN ('e', 'd', 'r', 'm', 'c')
          AND (t.typrelid = 0 OR (SELECT relkind FROM pg_class WHERE oid = t.typrelid) = 'c')
          AND t.typelem = 0
    LOOP
        EXECUTE format(
            CASE item.typtype WHEN 'd' THEN 'ALTER DOMAIN %s OWNER TO sentium_app' ELSE 'ALTER TYPE %s OWNER TO sentium_app' END,
            item.name);
    END LOOP;
END
$$;
