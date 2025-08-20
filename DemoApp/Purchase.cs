using System.ComponentModel.DataAnnotations;

namespace DemoApp;

public sealed class Purchase
{
    [Key]
    public long Id { get; set; }
}
