using System;
using System.Collections.Generic;
using System.Text;

namespace HomeLiveStore.Domain.Enums
{
  public enum OrderStatus : byte
  {
    Preparing = 1,
    Shipped = 2,
    Completed = 3,
    Canceled = 4
  }
}
