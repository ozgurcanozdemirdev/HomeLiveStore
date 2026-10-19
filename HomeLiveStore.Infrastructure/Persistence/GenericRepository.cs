#nullable disable

using System.Linq.Expressions;
using HomeLiveStore.Application.Interfaces.Services;
using HomeLiveStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HomeLiveStore.Infrastructure.Persistence
{
  public class GenericRepository<T>(HomeLiveStoreDbContext context) : IGenericRepository<T> where T : BaseEntity
  {
    public async Task EkleAsync(T entity, CancellationToken cancellationToken = default)
    {
      await context.Set<T>().AddAsync(entity, cancellationToken);
    }

    public async Task<T> GetAsync(Expression<Func<T, bool>> filtre = null, CancellationToken cancellationToken = default, params string[] includePaths)
    {
      return await BuildQuery(filtre, includePaths).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<T>> ListAsync(Expression<Func<T, bool>> filtre = null, CancellationToken cancellationToken = default, params string[] includePaths)
    {
      return await BuildQuery(filtre, includePaths).ToListAsync(cancellationToken);
    }

    public void Update(T entity)
    {
      context.Set<T>().Update(entity);
    }

    private IQueryable<T> BuildQuery(Expression<Func<T, bool>> filtre, params string[] includePaths)
    {
      IQueryable<T> query = context.Set<T>();
      foreach (var includePath in includePaths)
      {
        query = query.Include(includePath);
      }

      if (filtre is not null)
      {
        query = query.Where(filtre);
      }

      return query;
    }
  }
}
