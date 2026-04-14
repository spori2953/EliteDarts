namespace EliteDarts.Domain.Entities;

public class Board
{
    public int Id { get; set; }
    public int TournamentId { get; set; }
    public Tournament Tournament { get; set; } = null!;
    public int BoardNumber { get; set; }

}