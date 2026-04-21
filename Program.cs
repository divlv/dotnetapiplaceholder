using Microsoft.OpenApi.Models;
using Microsoft.Data.SqlClient;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "API App Placeholder",
        Version = "v1"
    });
});

var app = builder.Build();

var virtualApplicationPrefixes = new[]
{
    "/application",
    "/system"
};

// Support both local virtual path testing and App Service virtual application mounting.
app.Use((context, next) =>
{
    foreach (var prefix in virtualApplicationPrefixes)
    {
        if (context.Request.Path.StartsWithSegments(prefix, out var remainingPath))
        {
            context.Request.PathBase = context.Request.PathBase.Add(prefix);
            context.Request.Path = remainingPath;
            break;
        }
    }

    return next();
});

app.UseSwagger(options =>
{
    options.RouteTemplate = "swagger/{documentName}/swagger.json";
});

app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "swagger";
    options.SwaggerEndpoint("v1/swagger.json", "API App Placeholder v1");
});

app.MapGet("/", async (HttpContext context, IConfiguration configuration, CancellationToken cancellationToken) =>
    NormalizeVirtualApplication(context.Request.PathBase) switch
    {
        "system" => await GetSystemPlaceholderResponseAsync(configuration, cancellationToken),
        _ => Results.Text(
            $"This data was returned from API App - {Guid.NewGuid()}",
            "text/plain")
    })
    .WithName("GetApplicationPlaceholder");

app.Run();

static string NormalizeVirtualApplication(PathString pathBase)
{
    return pathBase.Value?.Trim('/').ToLowerInvariant() ?? string.Empty;
}

static async Task<IResult> GetSystemPlaceholderResponseAsync(IConfiguration configuration, CancellationToken cancellationToken)
{
    var baseConnectionString = configuration["DB_CONNECTION_STRING"];

    if (string.IsNullOrWhiteSpace(baseConnectionString))
    {
        return Results.Text(
            "This data was returned from API App - Database connection string DB_CONNECTION_STRING is missing.",
            "text/plain",
            statusCode: StatusCodes.Status500InternalServerError);
    }

    try
    {
        var connectionStringBuilder = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = "DSM_AZURE"
        };

        await using var connection = new SqlConnection(connectionStringBuilder.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var tables = await GetTablesAsync(connection, cancellationToken);

        if (tables.Count == 0)
        {
            return Results.Text(
                "This data was returned from API App - Database connected. No user tables were found in DSM_AZURE.",
                "text/plain");
        }

        var randomTable = tables[Random.Shared.Next(tables.Count)];
        var rowCount = await GetRowCountAsync(connection, randomTable, cancellationToken);

        var responseBuilder = new StringBuilder();
        responseBuilder.AppendLine("Tables in DSM_AZURE:");

        foreach (var table in tables)
        {
            responseBuilder.AppendLine(table.DisplayName);
        }

        responseBuilder.Append(
            $"This data was returned from API App - Database connected. Table {randomTable.DisplayName} has {rowCount} rows");

        return Results.Text(responseBuilder.ToString(), "text/plain");
    }
    catch (Exception exception)
    {
        return Results.Text(
            $"This data was returned from API App - Database request failed. {exception.Message}",
            "text/plain",
            statusCode: StatusCodes.Status500InternalServerError);
    }
}

static async Task<List<DatabaseTable>> GetTablesAsync(SqlConnection connection, CancellationToken cancellationToken)
{
    const string listTablesQuery = """
        SELECT s.name AS SchemaName, t.name AS TableName
        FROM sys.tables AS t
        INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
        WHERE t.is_ms_shipped = 0
        ORDER BY s.name, t.name;
        """;

    var tables = new List<DatabaseTable>();

    await using var command = new SqlCommand(listTablesQuery, connection);
    await using var reader = await command.ExecuteReaderAsync(cancellationToken);

    while (await reader.ReadAsync(cancellationToken))
    {
        tables.Add(new DatabaseTable(
            reader.GetString(0),
            reader.GetString(1)));
    }

    return tables;
}

static async Task<long> GetRowCountAsync(SqlConnection connection, DatabaseTable table, CancellationToken cancellationToken)
{
    var qualifiedTableName = $"{QuoteSqlIdentifier(table.SchemaName)}.{QuoteSqlIdentifier(table.TableName)}";
    var query = $"SELECT COUNT_BIG(*) FROM {qualifiedTableName};";

    await using var command = new SqlCommand(query, connection);
    var result = await command.ExecuteScalarAsync(cancellationToken);

    return Convert.ToInt64(result);
}

static string QuoteSqlIdentifier(string value)
{
    return $"[{value.Replace("]", "]]")}]";
}

internal sealed record DatabaseTable(string SchemaName, string TableName)
{
    public string DisplayName => $"{SchemaName}.{TableName}";
}
