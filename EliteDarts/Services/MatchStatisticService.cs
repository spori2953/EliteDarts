using EliteDarts.Data;
using EliteDarts.Domain.Entities;
using EliteDarts.Entities;
using Microsoft.EntityFrameworkCore;

namespace EliteDarts.Services
{
    public class MatchStatisticsService
    {
        private readonly ApplicationDbContext _db;

        public MatchStatisticsService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<MatchStatsDto> BuildMatchStatsAsync(int matchId)
        {
            var match = await _db.Matches
                .Include(m => m.Player1)
                .Include(m => m.Player2)
                .FirstOrDefaultAsync(m => m.Id == matchId);

            if (match is null)
                throw new Exception("A meccs nem található.");

            if (match.Player1Id is null || match.Player2Id is null || match.Player1 is null || match.Player2 is null)
                throw new Exception("A meccs nem teljes.");

            var scores = await _db.Scores
                .Where(s => s.MatchId == matchId)
                .OrderBy(s => s.LegNo)
                .ThenBy(s => s.CreatedAt)
                .ThenBy(s => s.Id)
                .ToListAsync();

            var dto = new MatchStatsDto
            {
                Player1Name = match.Player1.FullName,
                Player2Name = match.Player2.FullName,
                WinnerName = match.WinnerPlayerId == match.Player1Id ? match.Player1.FullName : match.Player2.FullName,
                FinalScoreText = $"{match.LegsPlayer1 ?? 0}:{match.LegsPlayer2 ?? 0}"
            };

            dto.Player1ThreeDartAverage = CalculateThreeDartAverage(scores, match.Player1Id.Value);
            dto.Player2ThreeDartAverage = CalculateThreeDartAverage(scores, match.Player2Id.Value);

            dto.Player1First9Average = CalculateFirst9Average(scores, match.Player1Id.Value);
            dto.Player2First9Average = CalculateFirst9Average(scores, match.Player2Id.Value);

            dto.Player1HighestCheckout = CalculateHighestCheckout(scores, match.Player1Id.Value);
            dto.Player2HighestCheckout = CalculateHighestCheckout(scores, match.Player2Id.Value);

            dto.Player1180Count = CountVisitsAtLeast(scores, match.Player1Id.Value, 180, exactOnly: true);
            dto.Player2180Count = CountVisitsAtLeast(scores, match.Player2Id.Value, 180, exactOnly: true);

            dto.Player1140PlusCount = CountVisitsAtLeast(scores, match.Player1Id.Value, 140, exactOnly: false);
            dto.Player2140PlusCount = CountVisitsAtLeast(scores, match.Player2Id.Value, 140, exactOnly: false);

            dto.Player1100PlusCount = CountVisitsAtLeast(scores, match.Player1Id.Value, 100, exactOnly: false);
            dto.Player2100PlusCount = CountVisitsAtLeast(scores, match.Player2Id.Value, 100, exactOnly: false);

            dto.Player1ShortestLegDarts = CalculateShortestLegDarts(scores, match.Player1Id.Value);
            dto.Player2ShortestLegDarts = CalculateShortestLegDarts(scores, match.Player2Id.Value);

            return dto;
        }

        private static double CalculateThreeDartAverage(List<Score> scores, int playerId)
        {
            var playerScores = scores
                .Where(s => s.PlayerId == playerId)
                .ToList();

            if (playerScores.Count == 0)
                return 0;

            // VISIT mód
            if (playerScores.Any(x => (x.Ring ?? "").ToUpperInvariant() == "VISIT"))
            {
                var visits = playerScores
                    .Where(x => (x.Ring ?? "").ToUpperInvariant() == "VISIT")
                    .Select(x => x.Points)
                    .ToList();

                if (visits.Count == 0) return 0;

                return visits.Average();
            }

            // DARTS mód
            var totalPoints = playerScores.Sum(s => s.Points);
            return (double)totalPoints / playerScores.Count * 3.0;
        }

        private static double CalculateFirst9Average(List<Score> scores, int playerId)
        {
            var playerScores = scores
                .Where(s => s.PlayerId == playerId)
                .OrderBy(s => s.LegNo)
                .ThenBy(s => s.CreatedAt)
                .ThenBy(s => s.Id)
                .ToList();

            if (playerScores.Count == 0)
                return 0;

            if (playerScores.Any(x => (x.Ring ?? "").ToUpperInvariant() == "VISIT"))
            {
                var firstVisits = playerScores
                    .Where(x => (x.Ring ?? "").ToUpperInvariant() == "VISIT")
                    .GroupBy(x => x.LegNo)
                    .SelectMany(g => g.Take(3))
                    .Select(x => x.Points)
                    .ToList();

                if (firstVisits.Count == 0) return 0;

                return firstVisits.Average();
            }

            // DARTS mód
            var first9 = playerScores
                .GroupBy(s => s.LegNo)
                .SelectMany(g => g.Take(9))
                .ToList();

            if (first9.Count == 0) return 0;

            var totalPoints = first9.Sum(s => s.Points);
            return (double)totalPoints / first9.Count * 3.0;
        }

        private static int CalculateHighestCheckout(List<Score> scores, int playerId)
        {
            var playerScores = scores
                .Where(s => s.PlayerId == playerId)
                .OrderBy(s => s.LegNo)
                .ThenBy(s => s.CreatedAt)
                .ThenBy(s => s.Id)
                .ToList();

            if (playerScores.Count == 0)
                return 0;

            int best = 0;

            // A checkout értéke = a checkout előtti maradék
            var visitScores = playerScores
                .Where(s => (s.Ring ?? "").ToUpperInvariant() == "VISIT")
                .ToList();

            if (visitScores.Count > 0)
            {
                foreach (var legGroup in visitScores.GroupBy(s => s.LegNo))
                {
                    int remaining = 501;
                    var visits = legGroup.OrderBy(s => s.CreatedAt).ThenBy(s => s.Id).ToList();

                    foreach (var visit in visits)
                    {
                        var points = visit.Points;
                        var remainingAfter = remaining - points;

                        if (remainingAfter == 0)
                        {
                            if (remaining > best)
                                best = remaining;

                            break;
                        }

                        // Bust
                        if (remainingAfter < 0 || remainingAfter == 1)
                        {
                            continue;
                        }

                        // Normál visit
                        remaining = remainingAfter;
                    }
                }

                return best;
            }

            // DARTS / CAMERA mód
            var legs = playerScores
                .Where(s => (s.Ring ?? "").ToUpperInvariant() != "VISIT")
                .GroupBy(s => s.LegNo);

            foreach (var leg in legs)
            {
                int remaining = 501;
                var list = leg.OrderBy(s => s.CreatedAt).ThenBy(s => s.Id).ToList();

                foreach (var s in list)
                {
                    var ring = (s.Ring ?? "").ToUpperInvariant();
                    var points = s.Points;
                    var remainingAfter = remaining - points;

                    bool isDoubleOut = ring == "D" || ring == "BULL";
                    bool isCheckout = remainingAfter == 0 && isDoubleOut;
                    bool isBust = remainingAfter < 0 || remainingAfter == 1 || (remainingAfter == 0 && !isDoubleOut);

                    if (isCheckout)
                    {
                        if (remaining > best)
                            best = remaining;

                        break;
                    }

                    if (isBust)
                    {
                        continue;
                    }

                    remaining = remainingAfter;
                }
            }
            return best;
        }

        private static int CountVisitsAtLeast(List<Score> scores, int playerId, int threshold, bool exactOnly)
        {
            var visits = BuildVisits(scores, playerId);

            if (exactOnly)
                return visits.Count(v => v == threshold);

            return visits.Count(v => v >= threshold);
        }

        private static int CalculateShortestLegDarts(List<Score> scores, int playerId)
        {
            var playerScores = scores
                .Where(s => s.PlayerId == playerId)
                .OrderBy(s => s.LegNo)
                .ThenBy(s => s.CreatedAt)
                .ThenBy(s => s.Id)
                .ToList();

            if (playerScores.Count == 0)
                return 0;

            var shortest = int.MaxValue;

            // VISIT mód
            var visitScores = playerScores
                .Where(s => (s.Ring ?? "").ToUpperInvariant() == "VISIT")
                .ToList();

            if (visitScores.Count > 0)
            {
                foreach (var legGroup in visitScores.GroupBy(s => s.LegNo))
                {
                    int remaining = 501;
                    int dartsThrown = 0;

                    var visits = legGroup.OrderBy(s => s.CreatedAt).ThenBy(s => s.Id).ToList();

                    foreach (var visit in visits)
                    {
                        var points = visit.Points;
                        var remainingAfter = remaining - points;

                        // bust
                        if (remainingAfter < 0 || remainingAfter == 1)
                        {
                            dartsThrown += 3;
                            continue;
                        }

                        // checkout
                        if (remainingAfter == 0)
                        {
                            // VISIT módban ThrowIndex tárolja, hogy hányadik nyíllal szállt ki
                            var checkoutDarts = visit.ThrowIndex;
                            if (checkoutDarts < 1 || checkoutDarts > 3)
                                checkoutDarts = 3;

                            dartsThrown += checkoutDarts;

                            if (dartsThrown < shortest)
                                shortest = dartsThrown;

                            break;
                        }

                        // normál visit
                        dartsThrown += 3;
                        remaining = remainingAfter;
                    }
                }

                return shortest == int.MaxValue ? 0 : shortest;
            }

            // DARTS / CAMERA mód
            foreach (var legGroup in playerScores
                .Where(s => (s.Ring ?? "").ToUpperInvariant() != "VISIT")
                .GroupBy(s => s.LegNo))
            {
                int remaining = 501;
                int dartsThrown = 0;

                var darts = legGroup.OrderBy(s => s.CreatedAt).ThenBy(s => s.Id).ToList();

                foreach (var dart in darts)
                {
                    var ring = (dart.Ring ?? "").ToUpperInvariant();
                    var points = dart.Points;
                    var remainingAfter = remaining - points;

                    bool isDoubleOut = ring == "D" || ring == "BULL";
                    bool isCheckout = remainingAfter == 0 && isDoubleOut;
                    bool isBust = remainingAfter < 0 || remainingAfter == 1 || (remainingAfter == 0 && !isDoubleOut);

                    dartsThrown++;

                    if (isCheckout)
                    {
                        if (dartsThrown < shortest)
                            shortest = dartsThrown;

                        break;
                    }

                    if (isBust)
                    {
                        continue;
                    }

                    remaining = remainingAfter;
                }
            }
            return shortest == int.MaxValue ? 0 : shortest;
        }

        private static List<int> BuildVisits(List<Score> scores, int playerId)
        {
            var playerScores = scores
                .Where(s => s.PlayerId == playerId)
                .OrderBy(s => s.LegNo)
                .ThenBy(s => s.CreatedAt)
                .ThenBy(s => s.Id)
                .ToList();

            var result = new List<int>();

            if (playerScores.Any(x => (x.Ring ?? "").ToUpperInvariant() == "VISIT"))
            {
                result.AddRange(playerScores
                    .Where(x => (x.Ring ?? "").ToUpperInvariant() == "VISIT")
                    .Select(x => x.Points));

                return result;
            }

            for (int i = 0; i < playerScores.Count; i += 3)
            {
                var chunk = playerScores.Skip(i).Take(3).ToList();
                result.Add(chunk.Sum(x => x.Points));
            }

            return result;
        }
    }
}