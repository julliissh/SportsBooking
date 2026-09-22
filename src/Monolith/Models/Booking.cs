namespace Monolith.Models;

public class Booking
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int VenueId { get; set; }
    public DateTime StartTime { get; set; }
    public int Hours { get; set; }
}
