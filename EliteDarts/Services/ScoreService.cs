using EliteDarts.Data;
using EliteDarts.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EliteDarts.Services;

public class ScoreService
{
    private readonly ApplicationDbContext _db;
    public ScoreService(ApplicationDbContext db) => _db = db;

    public static int CalculatePoints(string ring, int? sector)
    {
        ring = (ring ?? "").Trim().ToUpperInvariant();

        return ring switch
        {
            "BULL" => 50,
            "25" => 25,
            "S" => sector is >= 1 and <= 20 ? sector.Value : 0,
            "D" => sector is >= 1 and <= 20 ? sector.Value * 2 : 0,
            "T" => sector is >= 1 and <= 20 ? sector.Value * 3 : 0,
            _ => 0
        };
    }

    public async Task<List<Score>> GetScoresForMatchAsync(int matchId, int legNo = 1, int setNo = 1)
    {
        return await _db.Scores
            .Where(s => s.MatchId == matchId && s.LegNo == legNo && s.SetNo == setNo)
            .OrderBy(s => s.CreatedAt)
            .ThenBy(s => s.Id)
            .ToListAsync();
    }

    public async Task<int> AddThrowAsync(int matchId, int playerId, string ring, int? sector, ThrowSource source)
    {
        var count = await _db.Scores.CountAsync(s => s.MatchId == matchId && s.PlayerId == playerId && s.SetNo == 1 && s.LegNo == 1);
        var throwIndex = (count % 3) + 1;

        var points = CalculatePoints(ring, sector);

        var score = new Score
        {
            MatchId = matchId,
            PlayerId = playerId,
            SetNo = 1,
            LegNo = 1,
            ThrowIndex = throwIndex,
            Ring = ring.ToUpperInvariant(),
            Sector = sector,
            Points = points,
            Source = source,
            CreatedAt = DateTime.UtcNow
        };

        _db.Scores.Add(score);
        await _db.SaveChangesAsync();
        return score.Id;
    }

    public async Task UpdateThrowAsync(int scoreId, string ring, int? sector)
    {
        var score = await _db.Scores.FirstOrDefaultAsync(s => s.Id == scoreId);
        if (score is null) throw new Exception("Dobás nem található.");

        score.Ring = ring.ToUpperInvariant();
        score.Sector = sector;
        score.Points = CalculatePoints(ring, sector);

        score.Source = ThrowSource.Manual;

        await _db.SaveChangesAsync();
    }

    public async Task DeleteThrowAsync(int scoreId)
    {
        var score = await _db.Scores.FirstOrDefaultAsync(s => s.Id == scoreId);
        if (score is null) return;

        _db.Scores.Remove(score);
        await _db.SaveChangesAsync();
    }
}