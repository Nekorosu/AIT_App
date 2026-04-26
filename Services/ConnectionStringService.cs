using System.Text.Json;

namespace AIT_App.Services;

/// <summary>
/// Чтение и запись строки подключения в config.json рядом с исполняемым файлом.
/// </summary>
public static class ConnectionStringService
{
    // КОДЕР: путь к config.json в каталоге приложения (как в ТЗ).
    private static string ConfigPath =>
        Path.Combine(AppContext.BaseDirectory, "config.json");

    // ДИЗАЙНЕР: при отсутствии файла приложение всё равно стартует с шаблоном — можно подставить свой IP в Settings.
    private const string DefaultConnectionString =
        "Server=127.0.0.1;Port=3306;Database=electronic_journal;User=root;Password=;";

    private sealed record ConfigModel(string ConnectionString);

    /// <summary>
    /// Загружает строку подключения из config.json. Если файла нет — возвращает значение по умолчанию.
    /// </summary>
    public static string Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
                return DefaultConnectionString;

            var json = File.ReadAllText(ConfigPath);
            var model = JsonSerializer.Deserialize<ConfigModel>(json);
            return string.IsNullOrWhiteSpace(model?.ConnectionString)
                ? DefaultConnectionString
                : model.ConnectionString.Trim();
        }
        catch
        {
            return DefaultConnectionString;
        }
    }

    /// <summary>
    /// Сохраняет строку подключения в config.json (перезаписывает файл).
    /// </summary>
    public static void Save(string connectionString)
    {
        var model = new ConfigModel(connectionString.Trim());
        var json = JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }
}
