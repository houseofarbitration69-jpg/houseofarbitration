#region Imports
using Microsoft.EntityFrameworkCore;
using House.Of.Arbitration.Data.Abstractions;
#endregion

namespace House.Of.Arbitration.Data;

/// <summary>
/// 
/// </summary>
/// <typeparam name="T"></typeparam>
public class Repository<T> : IRepository<T> where T : class
{
    #region Services
    private readonly AppDbContext _context;
    #endregion

    #region Constructors
    /// <summary>
    /// Default constructor initialize dbContext
    /// </summary>
    /// <param name="context">Current dbContext</param>
    public Repository(AppDbContext context)
    {
        _context = context;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public async Task<T?> GetByIdAsync(int id)
    {
        return await _context.Set<T>().FindAsync(id);
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public async Task<IReadOnlyList<T>?> GetAllAsync()
    {
        return await _context.Set<T>().AsNoTracking().ToListAsync();
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public async Task<IReadOnlyList<T>?> GetAllAsync(params System.Linq.Expressions.Expression<Func<T, object>>[] includes)
    {
        IQueryable<T> query = _context.Set<T>().AsNoTracking();

        foreach (var include in includes)
        {
            query = query.Include(include);
        }

        return await query.ToListAsync();
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public async Task<T?> AddAsync(T entity)
    {
        await _context.Set<T>().AddAsync(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public async Task<bool> UpdateAsync(T entity)
    {
        _context.Entry(entity).State = EntityState.Modified;
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public async Task<bool> DeleteAsync(T entity)
    {
        _context.Set<T>().Remove(entity);
        await _context.SaveChangesAsync();

        return true;
    }
    #endregion
}
