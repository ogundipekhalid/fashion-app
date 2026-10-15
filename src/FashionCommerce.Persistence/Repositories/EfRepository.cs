using System.Linq.Expressions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Persistence.Repositories;

public class EfRepository<T> : IRepository<T> where T : class
{
    private readonly ApplicationDbContext _context;
    private readonly DbSet<T> _set;

    public EfRepository(ApplicationDbContext context)
    {
        _context = context;
        _set = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _set.FindAsync(new object[] { id }, cancellationToken);

    public async Task<List<T>> ListAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = _set;
        if (predicate is not null) query = query.Where(predicate);
        return await query.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default) =>
        await _set.AddAsync(entity, cancellationToken);

    public void Update(T entity) => _set.Update(entity);

    public void Remove(T entity) => _set.Remove(entity); // interceptor converts this to a soft delete for ISoftDelete entities
}
