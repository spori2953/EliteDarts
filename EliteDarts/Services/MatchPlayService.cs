using EliteDarts.Data;
using EliteDarts.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EliteDarts.Services;

public class MatchPlayService
{
    private readonly ApplicationDbContext _db;
    private readonly MatchResultService _matchResult;

    public MatchPlayService(ApplicationDbContext db, MatchResultService matchResult)
    {
        _db = db;
        _matchResult = matchResult;
    }

    public async Task StartMatchAsync(int matchId, int starterPlayerId, MatchInputMode mode)
    {
        var m = await _db.Matches.FirstOrDefaultAsync(x => x.Id == matchId);
        if (m is null) throw new Exception("Meccs nem található.");
        if (m.Player1Id is null || m.Player2Id is null) throw new Exception("BYE meccset nem lehet indítani.");

        m.Status = MatchStatus.Running;

        m.LegsPlayer1 ??= 0;
        m.LegsPlayer2 ??= 0;

        m.CurrentLegNo = 1;
        m.P1Remaining = 501;
        m.P2Remaining = 501;

        m.LegStarterPlayerId = starterPlayerId;
        m.CurrentPlayerId = starterPlayerId;

        m.CurrentDartInVisit = 0;
        m.VisitStartRemaining = 501;
        m.InputMode = mode;

        await _db.SaveChangesAsync();
    }

    public async Task<string> AddThrowAsync(int matchId, string ring, int? sector, ThrowSource source)
    {
        var m = await _db.Matches.FirstOrDefaultAsync(x => x.Id == matchId);
        if (m is null) throw new Exception("Meccs nem található.");
        if (m.Status != MatchStatus.Running) throw new Exception("A meccs nincs elindítva.");
        if (m.Player1Id is null || m.Player2Id is null) throw new Exception("BYE meccs.");

        if (m.PendingCheckoutConfirm)
            throw new Exception("Checkout megerősítés folyamatban van. Előbb erősítsd meg vagy vond vissza.");

        ring = (ring ?? "S").Trim().ToUpperInvariant();

        var currentPlayerId = m.CurrentPlayerId ?? m.Player1Id.Value;

        // visit eleje
        if (m.CurrentDartInVisit == 0)
            m.VisitStartRemaining = GetRemainingFor(m, currentPlayerId);

        var points = ScoreService.CalculatePoints(ring, sector);
        var remainingBefore = GetRemainingFor(m, currentPlayerId);
        var remainingAfter = remainingBefore - points;

        var score = new Score
        {
            MatchId = matchId,
            PlayerId = currentPlayerId,
            SetNo = 1,
            LegNo = m.CurrentLegNo,
            ThrowIndex = m.CurrentDartInVisit + 1,
            Ring = ring,
            Sector = sector,
            Points = points,
            Source = source,
            CreatedAt = DateTime.UtcNow
        };

        _db.Scores.Add(score);

        var isCheckout = remainingAfter == 0 && IsDoubleOut(ring);
        var isBust = remainingAfter < 0 || remainingAfter == 1 || (remainingAfter == 0 && !isCheckout);

        if (isBust)
        {
            await _db.SaveChangesAsync();

            // Az aktuális kör összes eddigi dobása legyen 0
            var currentVisitScores = await _db.Scores
                .Where(s =>
                    s.MatchId == matchId &&
                    s.PlayerId == currentPlayerId &&
                    s.SetNo == 1 &&
                    s.LegNo == m.CurrentLegNo &&
                    s.CreatedAt >= score.CreatedAt.AddSeconds(-30)) // biztonsági szűkítés
                .OrderByDescending(s => s.CreatedAt)
                .ThenByDescending(s => s.Id)
                .Take(m.CurrentDartInVisit + 1)
                .ToListAsync();

            foreach (var s in currentVisitScores)
                s.Points = 0;

            SetRemainingFor(m, currentPlayerId, m.VisitStartRemaining);
            EndTurnAndSwitchPlayer(m);

            await _db.SaveChangesAsync();
            return "BUST";
        }

        if (isCheckout)
        {
            SetRemainingFor(m, currentPlayerId, 0);

            m.PendingCheckoutConfirm = true;
            m.PendingCheckoutWinnerPlayerId = currentPlayerId;
            m.PendingCheckoutRing = ring;
            m.PendingCheckoutSector = sector;

            await _db.SaveChangesAsync();
            return "CHECKOUT_PENDING";
        }

        SetRemainingFor(m, currentPlayerId, remainingAfter);

        m.CurrentDartInVisit++;

        if (m.CurrentDartInVisit >= 3)
            EndTurnAndSwitchPlayer(m);

        await _db.SaveChangesAsync();
        return "OK";
    }

    public async Task<string> AddCvVisitAsync(int matchId, List<EliteDarts.Entities.DartDetection> darts)
    {
        if (darts is null || darts.Count == 0)
            throw new Exception("A kamera nem adott vissza dobást.");

        string lastResult = "OK";

        foreach (var dart in darts
            .OrderBy(d => d.DartNo))
        {
            if (!dart.Found || !dart.BaseNumber.HasValue || !dart.Multiplier.HasValue)
                continue;

            var baseNumber = dart.BaseNumber.Value;
            var multiplier = dart.Multiplier.Value;

            string ring = dart.Multiplier.Value switch
            {
                1 => dart.BaseNumber == 25 ? "25" : "S",
                2 => dart.BaseNumber == 25 ? "BULL" : "D",
                3 => "T",
                _ => throw new Exception("Érvénytelen CV multiplier.")
            };

            int? sector = dart.BaseNumber == 25 ? null : dart.BaseNumber;

            lastResult = await AddThrowAsync(matchId, ring, sector, ThrowSource.CV);

            if (lastResult == "BUST" || lastResult == "CHECKOUT_PENDING" || lastResult == "MATCH_FINISHED")
                break;
        }

        return lastResult;
    }

    private static bool IsDoubleOut(string ring)
    {
        ring = (ring ?? "").Trim().ToUpperInvariant();
        return ring == "D" || ring == "BULL";
    }

    private static int GetRemainingFor(Match m, int playerId)
        => (m.Player1Id == playerId) ? m.P1Remaining : m.P2Remaining;

    private static void SetRemainingFor(Match m, int playerId, int value)
    {
        if (m.Player1Id == playerId) m.P1Remaining = value;
        else m.P2Remaining = value;
    }

    private static void EndTurnAndSwitchPlayer(Match m)
    {
        m.CurrentDartInVisit = 0;
        m.CurrentPlayerId = (m.CurrentPlayerId == m.Player1Id) ? m.Player2Id : m.Player1Id;
    }

    private static void AddLegWin(Match m, int winnerId)
    {
        if (m.Player1Id == winnerId) m.LegsPlayer1 = (m.LegsPlayer1 ?? 0) + 1;
        else m.LegsPlayer2 = (m.LegsPlayer2 ?? 0) + 1;
    }

    private static void StartNextLeg(Match m)
    {
        m.CurrentLegNo++;

        m.P1Remaining = 501;
        m.P2Remaining = 501;

        // leg kezdés váltás
        m.LegStarterPlayerId = (m.LegStarterPlayerId == m.Player1Id) ? m.Player2Id : m.Player1Id;
        m.CurrentPlayerId = m.LegStarterPlayerId;

        m.CurrentDartInVisit = 0;
        m.VisitStartRemaining = 501;
    }

    public async Task ConfirmCheckoutAsync(int matchId, int dartNo)
    {
        if (dartNo < 1 || dartNo > 3)
            throw new Exception("A kiszálló nyíl száma 1 és 3 között lehet.");

        var m = await _db.Matches.FirstOrDefaultAsync(x => x.Id == matchId);
        if (m is null) throw new Exception("Meccs nem található.");
        if (m.Player1Id is null || m.Player2Id is null) throw new Exception("BYE meccs.");
        if (!m.PendingCheckoutConfirm || m.PendingCheckoutWinnerPlayerId is null)
            throw new Exception("Nincs megerősítésre váró checkout.");

        var winnerId = m.PendingCheckoutWinnerPlayerId.Value;

        var last = await _db.Scores
            .Where(s => s.MatchId == matchId && s.SetNo == 1 && s.LegNo == m.CurrentLegNo && s.PlayerId == winnerId)
            .OrderByDescending(s => s.CreatedAt)
            .ThenByDescending(s => s.Id)
            .FirstOrDefaultAsync();

        if (last is not null)
            last.ThrowIndex = dartNo;

        AddLegWin(m, winnerId);

        m.PendingCheckoutConfirm = false;
        m.PendingCheckoutWinnerPlayerId = null;
        m.PendingCheckoutDartNo = null;

        m.PendingCheckoutRing = null;
        m.PendingCheckoutSector = null;

        var winLegs = (m.BestOfLegs / 2) + 1;

        if ((m.LegsPlayer1 ?? 0) >= winLegs || (m.LegsPlayer2 ?? 0) >= winLegs)
        {
            await _db.SaveChangesAsync();
            await _matchResult.FinishMatchByLegsAsync(m.Id, m.LegsPlayer1 ?? 0, m.LegsPlayer2 ?? 0);
            return;
        }

        StartNextLeg(m);
        await _db.SaveChangesAsync();
    }

    public async Task CancelCheckoutAsync(int matchId)
    {
        var m = await _db.Matches.FirstOrDefaultAsync(x => x.Id == matchId);
        if (m is null) throw new Exception("Meccs nem található.");

        m.PendingCheckoutConfirm = false;
        m.PendingCheckoutWinnerPlayerId = null;
        m.PendingCheckoutRing = null;
        m.PendingCheckoutSector = null;

        await _db.SaveChangesAsync();
    }

    public async Task RebuildCurrentLegStateAsync(int matchId)
    {
        var m = await _db.Matches.FirstOrDefaultAsync(x => x.Id == matchId);
        if (m is null) throw new Exception("Meccs nem található.");
        if (m.Player1Id is null || m.Player2Id is null) throw new Exception("BYE meccs.");
        if (m.LegStarterPlayerId is null) throw new Exception("Nincs beállítva leg kezdő.");
        if (m.Status != MatchStatus.Running) throw new Exception("A meccs nem fut.");

        var legNo = m.CurrentLegNo;

        var scores = await _db.Scores
            .Where(s => s.MatchId == matchId && s.SetNo == 1 && s.LegNo == legNo)
            .OrderBy(s => s.CreatedAt)
            .ThenBy(s => s.Id)
            .ToListAsync();

        m.P1Remaining = 501;
        m.P2Remaining = 501;
        m.CurrentPlayerId = m.LegStarterPlayerId;
        m.CurrentDartInVisit = 0;
        m.VisitStartRemaining = 501;

        foreach (var s in scores)
        {
            var currentPlayerId = m.CurrentPlayerId ?? m.Player1Id.Value;
            s.Ring = (s.Ring ?? "S").ToUpperInvariant();

            // VISIT MÓD
            if (s.Ring == "VISIT")
            {
                var total = s.Points;

                if (total < 0 || total > 180)
                    total = 0;

                m.VisitStartRemaining = GetRemainingFor(m, currentPlayerId);

                var remainingBefore = GetRemainingFor(m, currentPlayerId);
                var remainingAfter = remainingBefore - total;

                s.ThrowIndex = 3;

                var isBust = remainingAfter < 0 || remainingAfter == 1;

                if (isBust)
                {
                    s.Points = 0;
                    SetRemainingFor(m, currentPlayerId, m.VisitStartRemaining);
                    EndTurnAndSwitchPlayer(m);
                    continue;
                }

                if (remainingAfter == 0)
                {
                    SetRemainingFor(m, currentPlayerId, 0);

                    m.PendingCheckoutConfirm = true;
                    m.PendingCheckoutWinnerPlayerId = currentPlayerId;
                    m.PendingCheckoutRing = null;
                    m.PendingCheckoutSector = null;

                    if (s.ThrowIndex is < 1 or > 3)
                        s.ThrowIndex = 3;

                    break;
                }

                // normál visit
                SetRemainingFor(m, currentPlayerId, remainingAfter);
                EndTurnAndSwitchPlayer(m);
                continue;
            }

            // NYILANKÉNTI MÓD
            if (m.CurrentDartInVisit == 0)
                m.VisitStartRemaining = GetRemainingFor(m, currentPlayerId);

            var points = ScoreService.CalculatePoints(s.Ring, s.Sector);
            s.Points = points;

            s.ThrowIndex = m.CurrentDartInVisit + 1;

            var remainingBefore2 = GetRemainingFor(m, currentPlayerId);
            var remainingAfter2 = remainingBefore2 - points;

            var isCheckout = remainingAfter2 == 0 && IsDoubleOut(s.Ring);
            var isBust2 = remainingAfter2 < 0 || remainingAfter2 == 1 || (remainingAfter2 == 0 && !isCheckout);

            if (isBust2)
            {
                // Az adott visit eddigi nyilai legyenek 0 pontosak
                var bustVisitScores = scores
                    .Where(x =>
                        x.PlayerId == currentPlayerId &&
                        x.LegNo == m.CurrentLegNo &&
                        x.SetNo == 1 &&
                        x.CreatedAt <= s.CreatedAt)
                    .OrderByDescending(x => x.CreatedAt)
                    .ThenByDescending(x => x.Id)
                    .Take(m.CurrentDartInVisit + 1)
                    .ToList();

                foreach (var bs in bustVisitScores)
                    bs.Points = 0;

                SetRemainingFor(m, currentPlayerId, m.VisitStartRemaining);
                EndTurnAndSwitchPlayer(m);
            }
            else if (isCheckout)
            {
                SetRemainingFor(m, currentPlayerId, 0);

                m.PendingCheckoutConfirm = true;
                m.PendingCheckoutWinnerPlayerId = currentPlayerId;
                m.PendingCheckoutRing = s.Ring;
                m.PendingCheckoutSector = s.Sector;

                break;
            }
            else
            {
                SetRemainingFor(m, currentPlayerId, remainingAfter2);
                m.CurrentDartInVisit++;

                if (m.CurrentDartInVisit >= 3)
                    EndTurnAndSwitchPlayer(m);
            }
        }

        await _db.SaveChangesAsync();
    }

    public async Task<string> AddVisitTotalAsync(int matchId, int total, ThrowSource source)
    {
        var m = await _db.Matches.FirstOrDefaultAsync(x => x.Id == matchId);
        if (m is null) throw new Exception("Meccs nem található.");
        if (m.Status != MatchStatus.Running) throw new Exception("A meccs nincs elindítva.");
        if (m.Player1Id is null || m.Player2Id is null) throw new Exception("BYE meccs.");
        if (m.PendingCheckoutConfirm) throw new Exception("Checkout megerősítés folyamatban van.");

        if (total < 0 || total > 180) throw new Exception("A visit pontszám 0 és 180 között lehet.");

        var currentPlayerId = m.CurrentPlayerId ?? m.Player1Id.Value;

        if (m.CurrentDartInVisit == 0)
            m.VisitStartRemaining = GetRemainingFor(m, currentPlayerId);

        var remainingBefore = GetRemainingFor(m, currentPlayerId);
        var remainingAfter = remainingBefore - total;

        var isBust = remainingAfter < 0 || remainingAfter == 1;

        _db.Scores.Add(new Score
        {
            MatchId = matchId,
            PlayerId = currentPlayerId,
            SetNo = 1,
            LegNo = m.CurrentLegNo,
            ThrowIndex = 3,
            Ring = "VISIT",
            Sector = null,
            Points = isBust ? 0 : total,
            Source = source,
            CreatedAt = DateTime.UtcNow
        });

        if (isBust)
        {
            SetRemainingFor(m, currentPlayerId, m.VisitStartRemaining);
            EndTurnAndSwitchPlayer(m);

            await _db.SaveChangesAsync();
            return "BUST";
        }

        if (remainingAfter == 0)
        {
            SetRemainingFor(m, currentPlayerId, 0);

            m.PendingCheckoutConfirm = true;
            m.PendingCheckoutWinnerPlayerId = currentPlayerId;
            m.PendingCheckoutDartNo = null;

            await _db.SaveChangesAsync();
            return "CHECKOUT_PENDING";
        }

        SetRemainingFor(m, currentPlayerId, remainingAfter);
        EndTurnAndSwitchPlayer(m);

        await _db.SaveChangesAsync();
        return "OK";
    }
}