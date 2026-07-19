-- Idempotent deployment helper for Baseera (SQL Server)
-- Usage: set connection externally; do not embed passwords in this script.
-- Example: sqlcmd -S ... -d Baseera -i scripts/deploy-db.sql

IF DB_ID(N'Baseera') IS NULL
BEGIN
    PRINT 'Create database Baseera externally before applying EF migrations.';
END
GO

-- Schema is owned by EF Core migrations.
-- Run: dotnet ef database update --project src/backend/Baseera.Infrastructure --startup-project src/backend/Baseera.Api
-- with BASEERA_CONNECTION set in the environment.
