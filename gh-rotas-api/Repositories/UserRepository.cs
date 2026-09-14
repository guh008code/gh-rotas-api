using System.Data.Common;
using gh_rotas_api.Models;
using Microsoft.Data.SqlClient;
using MySqlConnector;

namespace gh_rotas_api.Repositories;

public sealed class UserRepository(IConfiguration configuration) : IUserRepository
{
    public Task<User?> FindByEmail(string email, CancellationToken ct) =>
        Find("NormalizedEmail", email.Trim().ToUpperInvariant(), ct);

    public Task<User?> FindById(Guid id, CancellationToken ct) =>
        Find("Id", id.ToString(), ct);

    public async Task<bool> Create(User user, CancellationToken ct)
    {
        await using var connection = Connect();
        await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Users (
                Id, Name, Email, NormalizedEmail,
                Phone, PasswordHash, BirthDate, CreatedAtUtc
            )
            VALUES (
                @id, @name, @email, @normalizedEmail,
                @phone, @hash, @birth, @created
            )
            """;

        Add(cmd, "@id", user.Id.ToString());
        Add(cmd, "@name", user.Name);
        Add(cmd, "@email", user.Email);
        Add(cmd, "@normalizedEmail", user.Email.ToUpperInvariant());
        Add(cmd, "@phone", user.Phone);
        Add(cmd, "@hash", user.PasswordHash);
        Add(cmd, "@birth", user.BirthDate.ToDateTime(TimeOnly.MinValue));
        Add(cmd, "@created", DateTime.UtcNow);

        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
            return true;
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            return false;
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return false;
        }
    }

    private async Task<User?> Find(string column, string value, CancellationToken ct)
    {
        await using var connection = Connect();
        await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT Id, Name, Email, Phone, PasswordHash, BirthDate
            FROM Users
            WHERE {column} = @value
            """;
        Add(cmd, "@value", value);

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return new User(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            DateOnly.FromDateTime(reader.GetDateTime(5)));
    }

    private DbConnection Connect() =>
        configuration["Database:Provider"] == "SqlServer"
            ? new SqlConnection(configuration.GetConnectionString("DefaultConnection"))
            : new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));

    private static void Add(DbCommand cmd, string name, object value)
    {
        var parameter = cmd.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        cmd.Parameters.Add(parameter);
    }
}
