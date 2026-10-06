using Npgsql;
using Microsoft.EntityFrameworkCore;

namespace back.Data;

public static class PostgresConnection
{
    public static void Configure(DbContextOptionsBuilder options, string value, bool direct = false)
    {
        var connection = direct ? Direct(value) : Normalize(value);
        options.UseNpgsql(connection);
        var schema = new NpgsqlConnectionStringBuilder(connection).SearchPath;
        if (!string.IsNullOrEmpty(schema)) options.AddInterceptors(new SearchPathInterceptor(schema));
    }

    public static string Direct(string value)
    {
        var connection = new NpgsqlConnectionStringBuilder(Normalize(value));
        var host = connection.Host ?? throw new InvalidOperationException("Falta el host de PostgreSQL.");
        if (host.EndsWith(".neon.tech", StringComparison.OrdinalIgnoreCase))
            connection.Host = host.Replace("-pooler.", ".", StringComparison.OrdinalIgnoreCase);
        return connection.ConnectionString;
    }

    public static string Normalize(string value)
    {
        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        var uri = new Uri(value);
        var credentials = uri.UserInfo.Split(':', 2);
        if (credentials.Length != 2)
        {
            throw new InvalidOperationException("DATABASE_URL must include a username and password.");
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = Uri.UnescapeDataString(credentials[1]),
            SslMode = SslMode.Require
        };

        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && parts[0].Equals("channel_binding", StringComparison.OrdinalIgnoreCase))
            {
                builder.ChannelBinding = Enum.Parse<ChannelBinding>(parts[1], ignoreCase: true);
            }
        }

        return builder.ConnectionString;
    }
}
