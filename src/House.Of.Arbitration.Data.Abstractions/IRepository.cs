namespace House.Of.Arbitration.Data.Abstractions;

/// <summary>
/// Interface for all repository
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IRepository<T> where T : class
{
    /// <summary>
    /// Get item by id
    /// </summary>
    /// <param name="id">Item id</param>
    /// <returns>Item or null if not exist</returns>
    Task<T?> GetByIdAsync(int id);

    /// <summary>
    /// Get item by id
    /// </summary>
    /// <param name="id">Item id</param>
    /// <returns>Item or null if not exist</returns>
    Task<T?> GetByIdAsync(int id, params System.Linq.Expressions.Expression<Func<T, object>>[] includes);

    /// <summary>
    /// Get all items
    /// </summary>
    /// <returns>All items or null if empty</returns>
    Task<IReadOnlyList<T>?> GetAllAsync();

    /// <summary>
    /// Get all items with includes
    /// </summary>
    /// <param name="includes"></param>
    /// <returns></returns>
    Task<IReadOnlyList<T>?> GetAllAsync(params System.Linq.Expressions.Expression<Func<T, object>>[] includes);

    /// <summary>
    /// Add item
    /// </summary>
    /// <param name="entity">Item must be add</param>
    /// <returns>Item (with id) or null if error</returns>
    Task<T?> AddAsync(T entity);

    /// <summary>
    /// Update item
    /// </summary>
    /// <param name="entity">Item must be updated</param>
    /// <returns>true if update, false otherwise</returns>
    Task<bool> UpdateAsync(T entity);

    /// <summary>
    /// Delete item
    /// </summary>
    /// <param name="entity">Item must be deleted</param>
    /// <returns><c>true</c> if deleted <c>false</c> otherwise</returns>
    Task<bool> DeleteAsync(T entity);
}
