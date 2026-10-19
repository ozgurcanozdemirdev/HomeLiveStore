using System.ComponentModel.DataAnnotations;
using HomeLiveStore.Domain.Enums;

namespace HomeLiveStore.Domain.Entities
{
  public class User : BaseEntity
  {
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    public string Surname { get; set; } = string.Empty;

    [StringLength(256)]
    public string EMail { get; set; } = string.Empty;

    [StringLength(1000)]
    public string PasswordHash { get; set; } = string.Empty;

    public UserRole UserRole { get; set; } = UserRole.Customer;
    public ICollection<Order> Orders { get; set; } = new List<Order>();
  }
}
