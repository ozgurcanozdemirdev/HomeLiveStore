using System;
using System.Collections.Generic;
using System.Text;

namespace HomeLiveStore.Application.Common
{
  public class ResultObject<T>
  {
    public bool IsSuccess { get; set; }
    public string Message { get; set; }
    public T Data { get; set; }

    public static ResultObject<T> GenerateResult(T data, bool isSuccess, string message = "İşlem Başarılı")
    {
      return new ResultObject<T>()
      {
        Data = data,
        IsSuccess = isSuccess,
        Message = message
      };
    }
  }
}
