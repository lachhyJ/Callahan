namespace Callahan.Api.Models;

public class PushSubscription
{
    public int Id { get; set; }
    public required string Endpoint { get; set; }
    public required string P256dh { get; set; }
    public required string Auth { get; set; }
    public DateTime CreatedAt { get; set; }
}
