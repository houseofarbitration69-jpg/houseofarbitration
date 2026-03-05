#region Imports
using House.Of.Arbitration.Models;
using Microsoft.EntityFrameworkCore;
#endregion

namespace House.Of.Arbitration.Data;

/// <summary>
/// 
/// </summary>
public class AppDbContext : DbContext
{
    #region Properties
    public DbSet<CompetitionModel> Competitions { get; set; }
    public DbSet<CategoryModel> Categories{ get; set; }
    #endregion

    #region Constructors
    /// <summary>
    /// Default constructor, initialize sqlite database
    /// </summary>
    public AppDbContext()
    {
        Database.EnsureCreated();
    }
    #endregion

    #region Override Methods
    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    protected override void OnConfiguring(DbContextOptionsBuilder builder)
    {
        var dbPath = GetDbPath(Constants.LocalDatabase.DATABASE_NAME);
        builder.UseSqlite($"Filename={dbPath}");
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<CompetitionModel>(item =>
        {
            item.HasKey(i => i.Id);
            item.Property(i => i.Id).ValueGeneratedOnAdd();
        });

        builder.Entity<CategoryModel>(item =>
        {
            item.HasKey(i => i.Id);
            item.Property(i => i.Id).ValueGeneratedOnAdd();
            item.HasOne(i => i.Competition).WithMany().HasForeignKey(c => c.CompetitionId);
        });
    }
    #endregion

    #region Static Methods
    /// <summary>
    /// Get path to local database
    /// </summary>
    /// <returns></returns>
    public static string GetDbPath(string databaseName)
    {
        string path = String.Empty;

        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            path = Path.Combine(path, databaseName);
        }
        else if (DeviceInfo.Platform == DevicePlatform.iOS)
        {
            path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            path = Path.Combine(path, "..", "Library", databaseName);
        }
        else
        {
            path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            path = Path.Combine(path, databaseName);
        }

        return path;
    }
    #endregion
}
