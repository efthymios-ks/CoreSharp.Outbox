using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoreSharp.Outbox.Locks;

[Table("Locks", Schema = Constants.DatabaseSchema)]
[Index(nameof(Name), IsUnique = true)]
internal sealed class OutboxLock
{
    [Key]
    public required Guid Id { get; set; }

    public required string Name { get; set; }

    public required string AcquiredBy { get; set; }

    public required DateTimeOffset DateAcquired { get; set; }

    public required DateTimeOffset DateToExpire { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = default!;
}
