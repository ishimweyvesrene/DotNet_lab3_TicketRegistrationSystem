using System.Text.Json;
using MySqlConnector;

namespace TicketRegistrationDesktop.Services;

/// <summary>Settings read from appsettings.json (next to the .exe).</summary>
public class AppSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 3306;
    public string User { get; set; } = "root";
    public string Password { get; set; } = "";
    public string Database { get; set; } = "ticket_registration";
    public decimal ServiceFeePercent { get; set; } = 3;
    public string QrSecret { get; set; } = "change-me";
    public string AdminEmail { get; set; } = "admin@trs.local";
    public string AdminPassword { get; set; } = "Admin@12345";
    public bool SeedDemoData { get; set; } = true;

    public static AppSettings Current { get; private set; } = new();

    public static void Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path)) throw new FileNotFoundException("appsettings.json was not found next to the application.", path);
        Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path),
                      new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                  ?? throw new InvalidDataException("appsettings.json is empty or invalid.");
    }

    /// <summary>Connection string. Pass false to connect to the server without selecting the database (used to create it).</summary>
    public string BuildConnectionString(bool withDatabase = true) => new MySqlConnectionStringBuilder
    {
        Server = Host,
        Port = (uint)Port,
        UserID = User,
        Password = Password,
        Database = withDatabase ? Database : "",
        AllowUserVariables = true,
        ConnectionTimeout = 10
    }.ConnectionString;
}
