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
    public DbSet<CompetitorCategoryModel> CompetitorCategories { get; set; }
    public DbSet<AgeRangeModel> AgeRanges { get; set; }
    public DbSet<DrawModel> Draws { get; set; }
    public DbSet<DrawOrderModel> DrawsOrders { get; set; }
    public DbSet<DrawKnockoutModel> DrawsKnockouts { get; set; }
    public DbSet<DrawPoolsModel> DrawsPools { get; set; }
    public DbSet<RefereeDataModel> RefereeDatas { get; set; }
    public DbSet<WarningModel> Warnings { get; set; }
    public DbSet<CountryModel> Countries { get; set; }
    public DbSet<MvtTypeModel> MvtTypes { get; set; }
    public DbSet<MvtGroupModel> MvtGroupes { get; set; }
    public DbSet<MvtCodeModel> MvtCodes { get; set; }
    #endregion

    #region Constructors
    /// <summary>
    /// Default constructor, initialize sqlite database
    /// </summary>
    public AppDbContext()
    {
        InitializeDatabase();
    }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        Database.EnsureCreated();
        EnsureColumnExists(this, "Categories", "HasBronzeMatch", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumnExists(this, "DrawsKnockouts", "IsBronzeMatch", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumnExists(this, "RefereeDatas", "Round", "INTEGER NOT NULL DEFAULT 1");
        EnsureColumnExists(this, "RefereeDatas", "MatchTimer", "TEXT NULL");
        EnsureColumnExists(this, "DrawsKnockouts", "CurrentRound", "INTEGER NOT NULL DEFAULT 1");
        EnsureColumnExists(this, "DrawsPools", "CurrentRound", "INTEGER NOT NULL DEFAULT 1");
        EnsureColumnExists(this, "DrawsOrders", "CurrentRound", "INTEGER NOT NULL DEFAULT 1");
        EnsureColumnExists(this, "DrawsKnockouts", "RoundResultsJson", "TEXT NOT NULL DEFAULT ''");
        EnsureColumnExists(this, "DrawsPools", "RoundResultsJson", "TEXT NOT NULL DEFAULT ''");
        EnsureColumnExists(this, "DrawsOrders", "RoundResultsJson", "TEXT NOT NULL DEFAULT ''");
        EnsureColumnExists(this, "MvtCodes", "GroupName", "TEXT NOT NULL DEFAULT ''");
        EnsureColumnExists(this, "MvtCodes", "Movement", "TEXT NOT NULL DEFAULT ''");
    }

    private static void EnsureColumnExists(DbContext context, string tableName, string columnName, string columnDefinition)
    {
        try
        {
            var conn = context.Database.GetDbConnection();
            bool shouldClose = false;
            if (conn.State != System.Data.ConnectionState.Open)
            {
                conn.Open();
                shouldClose = !conn.ConnectionString.Contains(":memory:", StringComparison.OrdinalIgnoreCase);
            }

            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"PRAGMA table_info({tableName});";

                bool columnExists = false;
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var colName = reader.GetString(1);
                        if (string.Equals(colName, columnName, StringComparison.OrdinalIgnoreCase))
                        {
                            columnExists = true;
                            break;
                        }
                    }
                }

                if (!columnExists)
                {
                    using var alterCmd = conn.CreateCommand();
                    alterCmd.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
                    alterCmd.ExecuteNonQuery();
                }
            }
            finally
            {
                if (shouldClose)
                {
                    conn.Close();
                }
            }
        }
        catch
        {
            // Ignore if table does not exist yet or other non-critical error
        }
    }
    #endregion

    #region Override Methods
    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    protected override void OnConfiguring(DbContextOptionsBuilder builder)
    {
        if (!builder.IsConfigured)
        {
            var dbPath = GetDbPath(Constants.LocalDatabase.DATABASE_NAME);
            builder.UseSqlite($"Filename={dbPath}");
        }
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
            item.HasOne(i => i.Country)
                .WithMany()
                .HasForeignKey(i => i.CountryIsoCode);
        });

        builder.Entity<CountryModel>(item =>
        {
            item.HasKey(i => i.IsoCode);
            //item.HasData(CountryModel.DefaultCountries);
        });

        builder.Entity<CategoryModel>(item =>
        {
            item.HasKey(i => i.Id);
            item.Property(i => i.Id).ValueGeneratedOnAdd();
            item.HasOne(i => i.Competition).WithMany(c => c.Categories).HasForeignKey(c => c.CompetitionId);
            item.HasOne(i => i.AgeRange).WithMany().HasForeignKey(i => i.AgeRangeId);
            item.HasOne(i => i.Draw).WithOne(d => d.Category).HasForeignKey<DrawModel>(i => i.CategoryId);
        });

        builder.Entity<CompetitorCategoryModel>(item =>
        {
            item.HasKey(i => i.Id);
            item.Property(i => i.Id).ValueGeneratedOnAdd();
            item.HasOne(i => i.Competitor)
                .WithMany(c => c.Categories)
                .HasForeignKey(i => i.CompetitorId);
            item.HasOne(i => i.Category)
                .WithMany(c => c.Competitors)
                .HasForeignKey(i => i.CategoryId);
        });

        builder.Entity<AgeRangeModel>(item =>
        {
            item.HasKey(i => i.Id);
            item.Property(i => i.Id).ValueGeneratedOnAdd();
            //item.HasData(AgeRangeModel.DefaultRanges);
        });

        builder.Entity<DrawKnockoutModel>(item =>
        {
            item.HasKey(i => i.Id);
            item.Property(i => i.Id).ValueGeneratedOnAdd();
            item.HasOne(i => i.Draw)
                .WithMany(d => d.DrawKnockouts)
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
                .WithOne(r => r.DrawKnockoutModel)
                .HasForeignKey(r => r.DrawKnockoutId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DrawPoolsModel>(item =>
        {
            item.HasKey(i => i.Id);
            item.Property(i => i.Id).ValueGeneratedOnAdd();
            item.HasOne(i => i.Draw)
                .WithMany(d => d.DrawPools)
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
                .WithOne(r => r.DrawPools)
                .HasForeignKey(r => r.DrawPoolsId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DrawOrderModel>(item =>
        {
            item.HasKey(i => i.Id);
            item.Property(i => i.Id).ValueGeneratedOnAdd();
            item.HasOne(i => i.Draw)
                .WithMany(d => d.DrawOrders)
                .HasForeignKey(i => i.DrawId);

            item.HasOne(i => i.Competitor)
                .WithMany()
                .HasForeignKey(i => i.CompetitorId)
                .OnDelete(DeleteBehavior.Restrict);

            item.HasMany(i => i.RefereeDatas)
                .WithOne(r => r.DrawOrder)
                .HasForeignKey(r => r.DrawOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RefereeDataModel>(item =>
        {
            item.HasKey(i => i.Id);
            item.Property(i => i.Id).ValueGeneratedOnAdd();
            item.HasOne(i => i.Competitor)
                .WithMany()
                .HasForeignKey(i => i.CompetitorId);
        });

        builder.Entity<DrawModel>(item =>
        {
            item.HasKey(i => i.Id);
            item.Property(i => i.Id).ValueGeneratedOnAdd();
        });

        builder.Entity<WarningModel>(item =>
        {
            item.HasKey(i => i.Id);
            item.Property(i => i.Id).ValueGeneratedOnAdd();
            
            item.HasOne(i => i.Category)
                .WithMany()
                .HasForeignKey(i => i.CategoryId);

            item.HasOne(i => i.Competitor)
                .WithMany()
                .HasForeignKey(i => i.CompetitorId);

            item.HasOne<CompetitorCategoryModel>()
                .WithMany(cc => cc.Warnings)
                .HasForeignKey(i => i.CompetitorCategoryId);
        });

        builder.Entity<MvtGroupModel>(item =>
        {
            item.HasKey(i => i.Id);
            item.Property(i => i.Id).ValueGeneratedOnAdd();
        });

        builder.Entity<MvtTypeModel>(item =>
        {
            item.HasKey(i => i.Id);
            item.Property(i => i.Id).ValueGeneratedOnAdd();
        });

        builder.Entity<MvtCodeModel>(item =>
        {
            item.HasKey(i => i.Id);
            item.Property(i => i.Id).ValueGeneratedOnAdd();
            item.HasOne(i => i.Group)
                .WithMany(g => g.MvtCodes);
            item.HasOne(i => i.Type)
                .WithMany();
        });

        SeedData(builder);
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Vider la base de données des données insérées après l'installation tout en conservant les données par défaut.
    /// </summary>
    public async Task ResetUserDataAsync()
    {
        ChangeTracker.Clear();

        RefereeDatas.RemoveRange(RefereeDatas);
        Warnings.RemoveRange(Warnings);
        DrawsPools.RemoveRange(DrawsPools);
        DrawsKnockouts.RemoveRange(DrawsKnockouts);
        DrawsOrders.RemoveRange(DrawsOrders);
        Draws.RemoveRange(Draws);
        CompetitorCategories.RemoveRange(CompetitorCategories);
        Competitors.RemoveRange(Competitors);
        Categories.RemoveRange(Categories);
        Competitions.RemoveRange(Competitions);

        var defaultCountryCodes = CountryModel.DefaultCountries.Select(c => c.IsoCode).ToList();
        var customCountries = Countries.Where(c => !defaultCountryCodes.Contains(c.IsoCode));
        Countries.RemoveRange(customCountries);

        var defaultAgeRangeIds = AgeRangeModel.DefaultRanges.Select(a => a.Id).ToList();
        var customAgeRanges = AgeRanges.Where(a => !defaultAgeRangeIds.Contains(a.Id));
        AgeRanges.RemoveRange(customAgeRanges);

        var defaultGroupIds = MvtGroupModel.DefaultGroups.Select(g => g.Id).ToList();
        var customGroups = MvtGroupes.Where(g => !defaultGroupIds.Contains(g.Id));
        MvtGroupes.RemoveRange(customGroups);

        var defaultTypeIds = MvtTypeModel.DefaultTypes.Select(t => t.Id).ToList();
        var customTypes = MvtTypes.Where(t => !defaultTypeIds.Contains(t.Id));
        MvtTypes.RemoveRange(customTypes);

        var defaultCodeIds = MvtCodeModel.DefaultCodes.Select(c => c.Id).ToList();
        var customCodes = MvtCodes.Where(c => !defaultCodeIds.Contains(c.Id));
        MvtCodes.RemoveRange(customCodes);

        await SaveChangesAsync();
        ChangeTracker.Clear();
    }
    #endregion

    #region Private Methods
    private void SeedData(ModelBuilder builder)
    {
        builder.Entity<CountryModel>().HasData(CountryModel.DefaultCountries);
        builder.Entity<AgeRangeModel>().HasData(AgeRangeModel.DefaultRanges);
        builder.Entity<MvtGroupModel>().HasData(MvtGroupModel.DefaultGroups);
        builder.Entity<MvtTypeModel>().HasData(MvtTypeModel.DefaultTypes);
        builder.Entity<MvtCodeModel>().HasData(MvtCodeModel.DefaultCodes);
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
