using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace back.Data;

// Aplica explícitamente un esquema configurado en conexiones directas.
public sealed class SearchPathInterceptor(string schema) : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = new NpgsqlCommand("SELECT set_config('search_path', @schema, false)", (NpgsqlConnection)connection);
        command.Parameters.AddWithValue("schema", schema);
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var command = new NpgsqlCommand("SELECT set_config('search_path', @schema, false)", (NpgsqlConnection)connection);
        command.Parameters.AddWithValue("schema", schema);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
