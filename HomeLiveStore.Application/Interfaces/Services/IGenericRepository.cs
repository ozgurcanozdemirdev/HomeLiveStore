using HomeLiveStore.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace HomeLiveStore.Application.Interfaces.Services
{
  public interface IGenericRepository<T> where T : BaseEntity
  {
    Task<List<T>> ListAsync(Expression<Func<T, bool>> filtre = null, CancellationToken cancellationToken = default, params string[] includePaths);

    Task<T> GetAsync(Expression<Func<T, bool>> filtre = null, CancellationToken cancellationToken = default, params string[] includePaths);

    Task EkleAsync(T entity, CancellationToken cancellationToken = default);
    void Update(T entity);
  }
}
