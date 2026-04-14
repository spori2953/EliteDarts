using EliteDarts.Data;
using EliteDarts.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EliteDarts.Services;

public class BoardAssignmentService
{
    private readonly ApplicationDbContext _db;

    public BoardAssignmentService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task AssignAllMatchesToBoardsAsync(int tournamentId, bool onlyIfMissing = true)
    {
        var boards = await _db.Boards
            .Where(b => b.TournamentId == tournamentId)
            .OrderBy(b => b.BoardNumber)
            .ToListAsync();

        if (boards.Count == 0)
            throw new Exception("Nincs tábla létrehozva a versenyhez.");

        var query = _db.Matches
            .Where(m => m.TournamentId == tournamentId && m.Status != MatchStatus.Finished);

        if (onlyIfMissing)
            query = query.Where(m => m.BoardId == null);

        var matches = await query
            .OrderBy(m => m.RoundNo)
            .ThenBy(m => m.Id)
            .ToListAsync();

        if (matches.Count == 0)
            throw new Exception("Nincs kiosztható meccs.");

        for (int i = 0; i < matches.Count; i++)
        {
            matches[i].BoardId = boards[i % boards.Count].Id;
        }

        await _db.SaveChangesAsync();
    }

    public async Task SetMatchBoardAsync(int matchId, int? boardId)
    {
        var match = await _db.Matches.FirstOrDefaultAsync(m => m.Id == matchId);
        if (match is null) throw new Exception("Meccs nem található.");

        if (match.Status == MatchStatus.Finished)
            throw new Exception("Befejezett meccs táblája nem módosítható.");

        if (boardId.HasValue)
        {
            var exists = await _db.Boards.AnyAsync(b => b.Id == boardId.Value && b.TournamentId == match.TournamentId);
            if (!exists) throw new Exception("A kiválasztott tábla nem ehhez a versenyhez tartozik.");
        }

        match.BoardId = boardId;
        await _db.SaveChangesAsync();
    }
}