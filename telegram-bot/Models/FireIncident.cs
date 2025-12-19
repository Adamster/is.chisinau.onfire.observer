using Postgrest.Attributes;
using Postgrest.Models;

namespace TelegramBot.Models;

[Table("fire_incidents")]
public sealed class FireIncident : BaseModel
{
    [PrimaryKey("id", false)]
    public long Id { get; set; }

    [Column("datetime")]
    public DateTime Datetime { get; set; }

    [Column("photo_url")]
    public string PhotoUrl { get; set; } = string.Empty;

    [Column("street")]
    public string Street { get; set; } = string.Empty;
}
