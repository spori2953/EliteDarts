using EliteDarts.Data;
using EliteDarts.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using EliteDarts.Services;

namespace EliteDarts.Services;

public class BracketService
{
    private readonly ApplicationDbContext _db;

    

    private readonly BoardAssignmentService _boards;

    public BracketService(ApplicationDbContext db, BoardAssignmentService boards)
    {
        _db = db;
        _boards = boards;
    }

    public async Task GenerateSingleElimWithByesAsync(int tournamentId)
    {
        var tournament = await _db.Tournaments.FirstOrDefaultAsync(t => t.Id == tournamentId);
        if (tournament is null) throw new Exception("Nincs ilyen verseny.");

        var entries = await _db.Entries
            .Where(e => e.TournamentId == tournamentId)
            .Include(e => e.Player)
            .ToListAsync();

        var players = entries.Select(e => e.Player).ToList();
        if (players.Count < 2) throw new Exception("Legalább 2 nevező kell a sorsoláshoz.");

        // Ha már vannak meccsek, ne generáljuk újra
        var existingMatches = await _db.Matches.AnyAsync(m => m.TournamentId == tournamentId);
        if (existingMatches) throw new Exception("Ehhez a versenyhez már van sorsolás/meccs generálva.");

        // Véletlen sorrend
        var rng = new Random();
        players = players.OrderBy(_ => rng.Next()).ToList();

        int playerCount = players.Count;

        int rounds = (int)Math.Ceiling(Math.Log2(playerCount));
        int bracketSize = (int)Math.Pow(2, rounds);
        int byes = bracketSize - playerCount;

        var allMatches = new List<Match>();

        // Meccsek létrehozása körönként
        int matchesInRound = bracketSize / 2;
        for (int round = 1; round <= rounds; round++)
        {
            for (int i = 0; i < matchesInRound; i++)
            {
                allMatches.Add(new Match
                {
                    TournamentId = tournamentId,
                    RoundNo = round,
                    Status = MatchStatus.Pending
                });
            }

            matchesInRound /= 2;
        }

        _db.Matches.AddRange(allMatches);
        await _db.SaveChangesAsync();

        // NextMatch kapcsolatok beállítása
        for (int round = 1; round < rounds; round++)
        {
            var currentRound = allMatches.Where(m => m.RoundNo == round).ToList();
            var nextRound = allMatches.Where(m => m.RoundNo == round + 1).ToList();

            for (int i = 0; i < currentRound.Count; i++)
            {
                var currentMatch = currentRound[i];
                var nextMatch = nextRound[i / 2];

                currentMatch.NextMatchId = nextMatch.Id;
                currentMatch.IsWinnerGoesToPlayer1 = (i % 2 == 0); // 0->P1, 1->P2
            }
        }

        await _db.SaveChangesAsync();

        // 1. kör kiosztás BYE-okkal
        var firstRoundMatches = allMatches.Where(m => m.RoundNo == 1).ToList();
        int firstRoundCount = firstRoundMatches.Count;

        var byeFlags = Enumerable.Repeat(true, byes)
            .Concat(Enumerable.Repeat(false, firstRoundCount - byes))
            .OrderBy(_ => rng.Next())
            .ToList();

        var queue = new Queue<Player>(players);

        for (int i = 0; i < firstRoundCount; i++)
        {
            var match = firstRoundMatches[i];
            bool isBye = byeFlags[i];

            if (isBye)
            {
                var p = queue.Dequeue();
                bool putAsP1 = rng.Next(2) == 0;

                if (putAsP1)
                {
                    match.Player1Id = p.Id;
                    match.Player2Id = null;
                }
                else
                {
                    match.Player1Id = null;
                    match.Player2Id = p.Id;
                }
            }
            else
            {
                var p1 = queue.Dequeue();
                var p2 = queue.Dequeue();
                match.Player1Id = p1.Id;
                match.Player2Id = p2.Id;
            }
        }

        await _db.SaveChangesAsync();

        foreach (var match in firstRoundMatches)
        {
            bool hasP1 = match.Player1Id.HasValue;
            bool hasP2 = match.Player2Id.HasValue;

            if (hasP1 ^ hasP2)
            {
                var winnerId = hasP1 ? match.Player1Id : match.Player2Id;
                match.WinnerPlayerId = winnerId;
                match.Status = MatchStatus.Finished;

                if (winnerId.HasValue && match.NextMatchId.HasValue && match.IsWinnerGoesToPlayer1.HasValue)
                {
                    var next = allMatches.First(m => m.Id == match.NextMatchId.Value);

                    if (match.IsWinnerGoesToPlayer1.Value)
                        next.Player1Id = winnerId.Value;
                    else
                        next.Player2Id = winnerId.Value;
                }
            
            }

        }

        await _db.SaveChangesAsync();
        await _boards.AssignAllMatchesToBoardsAsync(tournamentId, onlyIfMissing: true);
    }
}