using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace RateWatch.Api.Tests;

/// <summary>
/// Runs the API against its own SQLite file in the temp folder, never the development database.
/// </summary>
public class RateWatchApiFactory : WebApplicationFactory<Program>
{
    private readonly bool _ownsDatabase;

    public RateWatchApiFactory()
        : this(NewTempDatabasePath(), ownsDatabase: true)
    {
    }

    private RateWatchApiFactory(string databasePath, bool ownsDatabase)
    {
        DatabasePath = databasePath;
        _ownsDatabase = ownsDatabase;
    }

    public string DatabasePath { get; }

    /// <summary>Uses an existing database file. The caller is responsible for deleting it.</summary>
    public static RateWatchApiFactory WithDatabaseFile(string databasePath) => new(databasePath, ownsDatabase: false);

    public static string NewTempDatabasePath() =>
        Path.Combine(Path.GetTempPath(), $"ratewatch-test-{Guid.NewGuid():N}.db");

    public static void DeleteDatabase(string databasePath)
    {
        SqliteConnection.ClearAllPools(); // pooled connections keep the file locked on Windows
        File.Delete(databasePath);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:RateWatch", $"Data Source={DatabasePath}");

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && _ownsDatabase)
        {
            DeleteDatabase(DatabasePath);
        }
    }
}
