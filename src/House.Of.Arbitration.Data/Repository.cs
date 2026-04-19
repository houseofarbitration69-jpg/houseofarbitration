using Microsoft.EntityFrameworkCore;
using House.Of.Arbitration.Data.Abstractions;

namespace House.Of.Arbitration.Data;

public class Repository<T> : IRepository<T> where T : class
{
    private readonly AppDbContext _context;

    public Repository(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Nettoie complètement le suivi du DbContext pour éviter les conflits dans MAUI.
    /// </summary>
    public void ClearTracker()
    {
        _context.ChangeTracker.Clear();
    }

    public async Task<T?> GetByIdAsync(int id)
    {
        return await _context.Set<T>().AsNoTrackingWithIdentityResolution().FirstOrDefaultAsync(e => EF.Property<int>(e, "Id") == id);
    }

    public async Task<T?> GetByIdAsync(int id, params System.Linq.Expressions.Expression<System.Func<T, object>>[] includes)
    {
        IQueryable<T> query = _context.Set<T>().AsNoTrackingWithIdentityResolution();
        foreach (var include in includes) query = query.Include(include);
        return await query.FirstOrDefaultAsync(e => EF.Property<int>(e, "Id") == id);
    }

    public async Task<T?> GetByIdAsync(int id, params string[] includePaths)
    {
        IQueryable<T> query = _context.Set<T>().AsNoTrackingWithIdentityResolution();
        foreach (var path in includePaths)
        {
            if (!string.IsNullOrWhiteSpace(path)) query = query.Include(path);
        }
        return await query.FirstOrDefaultAsync(e => EF.Property<int>(e, "Id") == id);
    }

    public async Task<IReadOnlyList<T>?> GetAllAsync()
    {
        return await _context.Set<T>().AsNoTrackingWithIdentityResolution().ToListAsync();
    }

    public async Task<IReadOnlyList<T>?> GetAllAsync(params System.Linq.Expressions.Expression<System.Func<T, object>>[] includes)
    {
        IQueryable<T> query = _context.Set<T>().AsNoTrackingWithIdentityResolution();
        foreach (var include in includes) query = query.Include(include);
        return await query.ToListAsync();
    }

    public async Task<IReadOnlyList<T>?> GetAllAsync(params string[] includePaths)
    {
        IQueryable<T> query = _context.Set<T>().AsNoTrackingWithIdentityResolution();
        foreach (var path in includePaths)
        {
            if (!string.IsNullOrWhiteSpace(path)) query = query.Include(path);
        }
        return await query.ToListAsync();
    }

    public async Task<T?> AddAsync(T entity)
    {
        ClearTracker();
        _context.Set<T>().Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task<bool> UpdateAsync(T entity)
    {
        ClearTracker();
        _context.Set<T>().Update(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(T entity)
    {
        // Nettoyage complet avant suppression pour éviter les conflits sur les enfants (Catégories, etc.)
        ClearTracker();
        
        _context.Set<T>().Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }
}
