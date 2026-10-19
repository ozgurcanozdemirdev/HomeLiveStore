namespace HomeLiveStore.Application.Exceptions
{
  public abstract class StoreException(string message) : Exception(message);

  public sealed class StoreNotFoundException(string message) : StoreException(message);

  public sealed class StoreValidationException(string message) : StoreException(message);

  public sealed class StoreConflictException(string message) : StoreException(message);

  public sealed class StoreUnauthorizedException(string message) : StoreException(message);
}
