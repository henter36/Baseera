using Baseera.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Baseera.IntegrationTests;

public abstract class SharedIntegrationFixture : IAsyncLifetime
{
    private const string BaselineTableName = "__IntegrationBaselineKeys";
    private readonly string _collectionKey;
    private IReadOnlyList<TableKeyDefinition> _tableKeys = [];

    protected SharedIntegrationFixture(string collectionKey)
    {
        _collectionKey = collectionKey;
    }

    public BaseeraApiFactory Factory { get; private set; } = null!;
    public string ConnectionString { get; private set; } = string.Empty;
    public string DatabaseName { get; private set; } = string.Empty;

    public HttpClient CreateAuthenticatedClient(
        string subject,
        string? displayName = null) =>
        Factory.CreateAuthenticatedClient(subject, displayName);

    public async Task InitializeAsync()
    {
        var raw = Environment.GetEnvironmentVariable("BASEERA_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        var builder = new SqlConnectionStringBuilder(raw)
        {
            InitialCatalog = $"Baseera_Test_{_collectionKey}_{Guid.NewGuid():N}"
        };

        ConnectionString = builder.ConnectionString;
        DatabaseName = builder.InitialCatalog;
        var options = new DbContextOptionsBuilder<BaseeraDbContext>()
            .UseSqlServer(ConnectionString, sql => sql.EnableRetryOnFailure(3))
            .AddInterceptors(new AuditImmutabilityInterceptor())
            .Options;

        await using (var db = new BaseeraDbContext(options))
        {
            await db.Database.MigrateAsync();
        }

        Factory = new BaseeraApiFactory(
            ConnectionString,
            applyMigrationsOnStartup: false,
            seedDemoOrganization: true);

        _ = Factory.Services;

        _tableKeys = await LoadTableKeysAsync();
        await CaptureBaselineAsync();
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        if (_tableKeys.Count == 0)
        {
            return;
        }

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await ExecuteNonQueryAsync(connection, DisableConstraintsSql(), cancellationToken);
        try
        {
            foreach (var table in _tableKeys)
            {
                await ExecuteNonQueryAsync(connection, BuildResetSql(table), cancellationToken);
            }
        }
        finally
        {
            await ExecuteNonQueryAsync(connection, EnableConstraintsSql(), cancellationToken);
        }
    }

    public async Task DisposeAsync()
    {
        if (Factory is null)
        {
            return;
        }

        try
        {
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            SqlConnection.ClearAllPools();
            await db.Database.EnsureDeletedAsync();
        }
        finally
        {
            await Factory.DisposeAsync();
        }
    }

    private async Task<IReadOnlyList<TableKeyDefinition>> LoadTableKeysAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) AS FullName,
                s.name + N'.' + t.name AS LogicalName,
                QUOTENAME(c.name) AS ColumnName,
                ic.key_ordinal AS KeyOrdinal
            FROM sys.tables t
            INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
            INNER JOIN sys.key_constraints kc
                ON kc.parent_object_id = t.object_id
               AND kc.[type] = 'PK'
            INNER JOIN sys.index_columns ic
                ON ic.object_id = kc.parent_object_id
               AND ic.index_id = kc.unique_index_id
            INNER JOIN sys.columns c
                ON c.object_id = ic.object_id
               AND c.column_id = ic.column_id
            WHERE t.is_ms_shipped = 0
              AND t.name NOT IN ('__EFMigrationsHistory', '__IntegrationBaselineKeys')
            ORDER BY FullName, ic.key_ordinal;
            """;

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var tables = new Dictionary<string, TableKeyBuilder>(StringComparer.Ordinal);
        while (await reader.ReadAsync(cancellationToken))
        {
            var fullName = reader.GetString(0);
            if (!tables.TryGetValue(fullName, out var table))
            {
                table = new TableKeyBuilder(fullName, reader.GetString(1));
                tables.Add(fullName, table);
            }

            table.Columns.Add(reader.GetString(2));
        }

        return tables.Values
            .Select(table => new TableKeyDefinition(table.FullName, table.LogicalName, table.Columns))
            .ToArray();
    }

    private async Task CaptureBaselineAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await ExecuteNonQueryAsync(
            connection,
            $"""
            IF OBJECT_ID(N'dbo.{BaselineTableName}', N'U') IS NOT NULL
                DROP TABLE dbo.{BaselineTableName};

            CREATE TABLE dbo.{BaselineTableName}
            (
                TableName nvarchar(256) NOT NULL,
                KeyValue nvarchar(1024) NOT NULL,
                CONSTRAINT PK_{BaselineTableName} PRIMARY KEY (TableName, KeyValue)
            );
            """,
            cancellationToken);

        foreach (var table in _tableKeys)
        {
            await ExecuteNonQueryAsync(connection, BuildCaptureSql(table), cancellationToken);
        }
    }

    private static string BuildCaptureSql(TableKeyDefinition table) =>
        $"""
        INSERT INTO dbo.{BaselineTableName} (TableName, KeyValue)
        SELECT N'{table.LogicalName}', {BuildKeyExpression(table.Columns)}
        FROM {table.FullName};
        """;

    private static string BuildResetSql(TableKeyDefinition table) =>
        $"""
        DELETE target
        FROM {table.FullName} AS target
        WHERE NOT EXISTS
        (
            SELECT 1
            FROM dbo.{BaselineTableName} AS baseline
            WHERE baseline.TableName = N'{table.LogicalName}'
              AND baseline.KeyValue = {BuildKeyExpression(table.Columns, "target")}
        );
        """;

    private static string BuildKeyExpression(IReadOnlyList<string> columns, string? alias = null)
    {
        var prefix = alias is null ? string.Empty : $"{alias}.";
        return string.Join(
            " + N'|' + ",
            columns.Select(column => $"CONVERT(nvarchar(256), {prefix}{column})"));
    }

    private static string DisableConstraintsSql() =>
        """
        EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';
        EXEC sp_MSforeachtable 'DISABLE TRIGGER ALL ON ?';
        """;

    private static string EnableConstraintsSql() =>
        """
        EXEC sp_MSforeachtable 'ENABLE TRIGGER ALL ON ?';
        EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';
        """;

    private static async Task ExecuteNonQueryAsync(
        SqlConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(sql, connection)
        {
            CommandTimeout = 120
        };
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed record TableKeyDefinition(
        string FullName,
        string LogicalName,
        IReadOnlyList<string> Columns);

    private sealed record TableKeyBuilder(string FullName, string LogicalName)
    {
        public List<string> Columns { get; } = [];
    }
}

public abstract class IntegrationTestBase<TFixture> : IAsyncLifetime
    where TFixture : SharedIntegrationFixture
{
    protected IntegrationTestBase(TFixture fixture)
    {
        Fixture = fixture;
    }

    protected TFixture Fixture { get; }
    protected BaseeraApiFactory Factory => Fixture.Factory;

    public Task InitializeAsync() => Fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;
}
