namespace HomeLiveStore.Domain.Entities
{
  public class BaseEntity
  {
    public int Id { get; set; }
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastModifedOn { get; set; }
    public bool Active { get; set; } = true;
    public bool Deleted { get; set; }
  }
}
