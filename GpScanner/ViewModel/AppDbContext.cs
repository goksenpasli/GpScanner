using GpScanner.Properties;
using System.Data.Entity;
using System.Data.SQLite;

namespace GpScanner.ViewModel;

public class AppDbContext : DbContext
{
    public AppDbContext() : base(new SQLiteConnection() { ConnectionString = new SQLiteConnectionStringBuilder() { DataSource = Settings.Default.DatabaseFile, ForeignKeys = true }.ConnectionString }, true)
    {
    }

    public DbSet<Data> Data { get; set; }

    public DbSet<ReminderData> ReminderData { get; set; }
}