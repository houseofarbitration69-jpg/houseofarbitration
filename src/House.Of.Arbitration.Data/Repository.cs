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

    public async Task<T?> GetByIdAsync(int id)
    {
        return await _context.Set<T>().AsNoTracking().FirstOrDefaultAsync(e => EF.Property<int>(e, "Id") == id);
    }

    public async Task<T?> GetByIdAsync(int id, params System.Linq.Expressions.Expression<System.Func<T, object>>[] includes)
    {
        IQueryable<T> query = _context.Set<T>().AsNoTracking();

        foreach (var include in includes)
        {
            query = query.Include(include);
        }

        return await query.FirstOrDefaultAsync(e => EF.Property<int>(e, "Id") == id);
    }

    public async Task<IReadOnlyList<T>?> GetAllAsync()
    {
        return await _context.Set<T>().AsNoTracking().ToListAsync();
    }

    public async Task<IReadOnlyList<T>?> GetAllAsync(params System.Linq.Expressions.Expression<System.Func<T, object>>[] includes)
    {
        IQueryable<T> query = _context.Set<T>().AsNoTracking();

        foreach (var include in includes)
        {
            query = query.Include(include);
        }

        return await query.ToListAsync();
    }

    public async Task<T?> AddAsync(T entity)
    {
        await _context.Set<T>().AddAsync(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task<bool> UpdateAsync(T entity)
    {
        // On détache toute instance déjà suivie avec le même ID pour éviter les conflits
        var idProp = entity.GetType().GetProperty("Id");
        if (idProp != null)
        {
            int id = (int)idProp.GetValue(entity)!;
            var trackedEntity = _context.Set<T>().Local.FirstOrDefault(e => (int)e.GetType().GetProperty("Id")!.GetValue(e)! == id);
            
            if (trackedEntity != null)
            {
                _context.Entry(trackedEntity).State = EntityState.Detached;
            }
        }

        // UTILISATION DE Update() AU LIEU DE State = Modified
        // Update() parcourt les relations et ajoute les nouvelles entités (états Added/Modified automatiques)
        _context.Update(entity);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeleteAsync(T entity)
    {
        _context.Set<T>().Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }
}
