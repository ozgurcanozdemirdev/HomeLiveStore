namespace HomeLiveStore.Infrastructure.Services
{
  public interface IStoreDatabaseInitializer
  {
    Task InitializeAsync(CancellationToken cancellationToken = default);
  }
}
