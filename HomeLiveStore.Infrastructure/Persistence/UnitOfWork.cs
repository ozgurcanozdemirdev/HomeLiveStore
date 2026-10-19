using HomeLiveStore.Application.Interfaces.Services;

namespace HomeLiveStore.Infrastructure.Persistence
{
  public class UnitOfWork(HomeLiveStoreDbContext context) : IUnitOfWork
  {
    public Task<int> SaveAsync(CancellationToken cancellationToken = default)
    {
      return context.SaveChangesAsync(cancellationToken);
    }
  }
}
