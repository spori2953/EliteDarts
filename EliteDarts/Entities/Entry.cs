namespace EliteDarts.Domain.Entities;

public class Entry
{
    public int Id { get; set; }
    public int TournamentId { get; set; }
    public Tournament Tournament { get; set; } = null!;
    public int PlayerId { get; set; }
    public Player Player { get; set; } = null!;
    public int? Seed { get; set; }
}