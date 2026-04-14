using EliteDarts.Data;
using EliteDarts.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EliteDarts.Services;

public class MatchResultService
{
    private readonly ApplicationDbContext _db;

    public MatchResultService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task FinishMatchByLegsAsync(int matchId, int legsP1, int legsP2)
    {
        if (legsP1 < 0 || legsP2 < 0)
            throw new Exception("A legszám nem lehet negatív.");

        if (legsP1 == legsP2)
            throw new Exception("Döntetlen nem lehet.");

        if (legsP1 == 0 && legsP2 == 0)
            throw new Exception("0-0 eredményt nem lehet rögzíteni.");

        var match = await _db.Matches.FirstOrDefaultAsync(m => m.Id == matchId);
        if (match is null) throw new Exception("Meccs nem található.");

        if (match.Player1Id is null || match.Player2Id is null)
            throw new Exception("BYE meccset nem kell kézzel lezárni.");

        var bestOf = match.BestOfLegs <= 0 ? 5 : match.BestOfLegs;
        var winLegs = (bestOf / 2) + 1;

        var maxLegs = Math.Max(legsP1, legsP2);
        var minLegs = Math.Min(legsP1, legsP2);

        if (maxLegs != winLegs)
            throw new Exception($"Ehhez a meccshez a győztesnek pontosan {winLegs} leget kell elérnie (Best of {bestOf}).");

        if (legsP1 >= winLegs && legsP2 >= winLegs)
            throw new Exception("Ilyen eredmény nem lehetséges (mindkét fél elérte a győztes legszámot).");


        if (minLegs >= winLegs)
            throw new Exception("A vesztes oldal túl nagy érték, ellenőrizd a legarányt.");

        if (minLegs > winLegs - 1)
            throw new Exception($"Ehhez a meccshez a vesztes legszáma maximum {winLegs - 1} lehet.");

        var winnerId = (legsP1 > legsP2) ? match.Player1Id.Value : match.Player2Id.Value;

        match.LegsPlayer1 = legsP1;
        match.LegsPlayer2 = legsP2;
        match.WinnerPlayerId = winnerId;
        match.Status = MatchStatus.Finished;

        if (match.NextMatchId.HasValue && match.IsWinnerGoesToPlayer1.HasValue)
        {
            var next = await _db.Matches.FirstOrDefaultAsync(m => m.Id == match.NextMatchId.Value);
            if (next is null) throw new Exception("Következő meccs nem található.");

            if (match.IsWinnerGoesToPlayer1.Value)
                next.Player1Id = winnerId;
            else
                next.Player2Id = winnerId;
        }

        await _db.SaveChangesAsync();
    }

}