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
    public DbSet<CompetitorModel> Competitors { get; set; }
    public DbSet<DrawModel> Draws { get; set; }
    public DbSet<DrawTaoluModel> DrawsTaolus { get; set; }
    public DbSet<DrawSandaModel> DrawsSandas { get; set; }
    public DbSet<RefereeDataModel> RefereeDatas { get; set; }
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

        builder.Entity<CompetitorModel>(item =>
        {
            item.HasKey(i => i.Id);
            item.Property(i => i.Id).ValueGeneratedOnAdd();
            item.HasOne(i => i.Category).WithMany(c => c.Competitors).HasForeignKey(c => c.CategoryId);
        });

        builder.Entity<CategoryModel>(item =>
        {
            item.HasKey(i => i.Id);
            item.Property(i => i.Id).ValueGeneratedOnAdd();
            item.HasOne(i => i.Competition).WithMany(c => c.Categories).HasForeignKey(c => c.CompetitionId);
        });

        builder.Entity<DrawSandaModel>(item =>
        {
            item.HasKey(i => i.Id);
            item.Property(i => i.Id).ValueGeneratedOnAdd();
            item.HasOne(i => i.Draw)
                .WithMany(d => d.DrawSandas)
                .HasForeignKey(i => i.DrawId);

            item.HasOne(i => i.Competitor1)
                .WithMany()
                .HasForeignKey(i => i.Competitor1Id)
                .OnDelete(DeleteBehavior.Restrict);

            item.HasOne(i => i.Competitor2)
                .WithMany()
                .HasForeignKey(i => i.Competitor2Id)
                .OnDelete(DeleteBehavior.Restrict);

            item.HasOne(i => i.Winner)
                .WithMany()
                .HasForeignKey(i => i.WinnerId)
                .OnDelete(DeleteBehavior.Restrict);

            item.HasOne(i => i.Looser)
                .WithMany()
                .HasForeignKey(i => i.LooserId)
                .OnDelete(DeleteBehavior.Restrict);

            item.HasMany(i => i.RefereeDatas)
                .WithOne()
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DrawTaoluModel>(item =>
        {
            item.HasKey(i => i.Id);
            item.Property(i => i.Id).ValueGeneratedOnAdd();
            item.HasOne(i => i.Draw)
                .WithMany(d => d.DrawTaolus)
                .HasForeignKey(i => i.DrawId);

            item.HasMany(i => i.RefereeDatas)
                .WithOne()
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RefereeDataModel>(item =>
        {
            item.HasKey(i => i.Id);
            item.Property(i => i.Id).ValueGeneratedOnAdd();
        });

        builder.Entity<DrawModel>(item =>
        {
            item.HasKey(i => i.Id);
            item.Property(i => i.Id).ValueGeneratedOnAdd();
            item.HasOne(i => i.Category)
                .WithMany()
                .HasForeignKey(i => i.CategoryId);
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
