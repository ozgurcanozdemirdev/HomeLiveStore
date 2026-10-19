using System;
using System.Collections.Generic;
using System.Text;

namespace HomeLiveStore.Application.Interfaces.Services
{
  public interface IUnitOfWork
  {
    Task<int> SaveAsync(CancellationToken cancellationToken = default);
  }
}
