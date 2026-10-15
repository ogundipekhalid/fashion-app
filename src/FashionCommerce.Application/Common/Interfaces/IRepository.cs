using System.Linq.Expressions;

namespace FashionCommerce.Application.Common.Interfaces;

/// <summary>
/// Generic repository, layered on top of IApplicationDbContext for the
/// common cases (get by id, list with a filter). Most CQRS handlers query
/// IApplicationDbContext's DbSets directly for full LINQ flexibility —
/// this exists for the simpler handlers and to satisfy the Repository
/// Pattern requirement explicitly. "Unit of Work" is IApplicationDbContext's
/// SaveChangesAsync — a separate IUnitOfWork wrapper would just be a proxy
/// for it, so it's deliberately not duplicated.
/// </summary>
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<T>> ListAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default);
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    void Update(T entity);
    void Remove(T entity);
}
