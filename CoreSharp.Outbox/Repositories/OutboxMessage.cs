using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoreSharp.Outbox.Repositories;

[Table("Messages", Schema = Constants.DatabaseSchema)]
[Index(nameof(DateProcessed), nameof(DateOccured))]
internal sealed class OutboxMessage
{
    [Key]
    [Column("Id")]
    public required Guid Id { get; init; }

    public required string MessageType { get; init; }

    public required string Payload { get; init; }

    public required DateTimeOffset DateOccured { get; init; }

    public DateTimeOffset? DateProcessed { get; set; }

    public string? Error { get; set; }
}
