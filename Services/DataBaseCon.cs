using System.Data;
using System.Diagnostics;
using MySqlConnector;

namespace AIT_App.Services;

/// <summary>
/// Обёртка над MySqlConnector (ADO.NET).
/// КОДЕР: все методы — асинхронные. Вызывайте через await, иначе UI замёрзнет
/// на сетевом запросе к удалённой БД (192.168.x.x).
/// При ошибке методы возвращают null/-1 И записывают сообщение в <see cref="LastError"/>,
/// плюс пишут полный стек в Debug Output (View → Tool Windows → Debug в Rider).
/// </summary>
public sealed class DataBaseCon
{
    private readonly string _connectionString;

    /// <summary>
    /// Сообщение последней ошибки SQL. null — последний вызов прошёл успешно.
    /// КОДЕР: показывайте пользователю при rc &lt; 0 / null-результате,
    /// чтобы тестировщик мог писать вменяемые баг-репорты.
    /// </summary>
    public string? LastError { get; private set; }

    public DataBaseCon() : this(ConnectionStringService.Load()) { }

    public DataBaseCon(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>SELECT → DataTable. null при ошибке (см. <see cref="LastError"/>).</summary>
    public async Task<DataTable?> ExecuteQueryAsync(string sql, Dictionary<string, object?>? parameters = null)
    {
        LastError = null;
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            AddParameters(command, parameters);
            await using var reader = await command.ExecuteReaderAsync();
            var table = new DataTable();
            table.Load(reader);
            return table;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Debug.WriteLine($"[DB] ExecuteQuery FAIL\nSQL: {sql}\n{ex}");
            return null;
        }
    }

    public async Task<object?> ExecuteScalarAsync(string sql, Dictionary<string, object?>? parameters = null)
    {
        LastError = null;
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            AddParameters(command, parameters);
            return await command.ExecuteScalarAsync();
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Debug.WriteLine($"[DB] ExecuteScalar FAIL\nSQL: {sql}\n{ex}");
            return null;
        }
    }

    /// <summary>
    /// Возвращает: число затронутых строк; -2 при дубликате уникального ключа (MySQL 1062);
    /// -1 при прочей ошибке (см. <see cref="LastError"/>).
    /// </summary>
    public async Task<int> ExecuteNonQueryAsync(string sql, Dictionary<string, object?>? parameters = null)
    {
        LastError = null;
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new MySqlCommand(sql, connection);
            AddParameters(command, parameters);
            return await command.ExecuteNonQueryAsync();
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            LastError = ex.Message;
            Debug.WriteLine($"[DB] ExecuteNonQuery DUPLICATE\nSQL: {sql}\n{ex}");
            return -2;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Debug.WriteLine($"[DB] ExecuteNonQuery FAIL\nSQL: {sql}\n{ex}");
            return -1;
        }
    }

    /// <summary>Проверка текущей строки подключения.</summary>
    public Task<(bool Ok, string? Message)> ConnectionCheckAsync() => CheckStaticAsync(_connectionString);

    /// <summary>Проверка произвольной строки подключения (асинхронная — для AuthWindow и SettingsWindow).</summary>
    public static async Task<(bool Ok, string? Message)> CheckStaticAsync(string connectionString)
    {
        try
        {
            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DB] CheckStatic FAIL\n{ex}");
            return (false, ex.Message);
        }
    }

    private static void AddParameters(MySqlCommand command, Dictionary<string, object?>? parameters)
    {
        if (parameters is null)
            return;

        foreach (var (name, value) in parameters)
        {
            var paramName = name.StartsWith('@') ? name : "@" + name;
            command.Parameters.AddWithValue(paramName, value ?? DBNull.Value);
        }
    }
}
