namespace EliteDarts.CvWorker.Services;

public static class DartScoring
{
    public static readonly int[] SectorOrderClockwiseFrom20 = new[]
    {
        20, 1, 18, 4, 13, 6, 10, 15, 2, 17,
        3, 19, 7, 16, 8, 11, 14, 9, 12, 5
    };

    public record DartScoreResult(
        bool IsMiss,
        int BaseNumber,
        int Multiplier,
        int Score,
        string Label
    );

    public static DartScoreResult CalculateDartScore(
        double tipX, double tipY,
        int boardCx, int boardCy, int boardR,
        double rotationDegCWFromTop = 0.0)
    {
        var dx = tipX - boardCx;
        var dy = tipY - boardCy;

        var dist = Math.Sqrt(dx * dx + dy * dy);
        var rNorm = dist / boardR;

        const double innerBull = 6.35 / 170.0;
        const double outerBull = 15.9 / 170.0;
        const double tripleIn = 99.0 / 170.0;
        const double tripleOut = 107.0 / 170.0;
        const double doubleIn = 162.0 / 170.0;
        const double doubleOut = 170.0 / 170.0;

        if (rNorm > doubleOut * 1.02)
            return new DartScoreResult(true, 0, 0, 0, "MISS");

        if (rNorm <= innerBull)
            return new DartScoreResult(false, 25, 2, 50, "DBULL (50)");

        if (rNorm <= outerBull)
            return new DartScoreResult(false, 25, 1, 25, "SBULL (25)");

        var ang = Math.Atan2(dx, -dy) * 180.0 / Math.PI;
        if (ang < 0) ang += 360.0;
        ang = (ang - rotationDegCWFromTop + 360.0) % 360.0;

        var sectorIndex = (int)Math.Floor((ang + 9.0) / 18.0) % 20;
        var baseNum = SectorOrderClockwiseFrom20[sectorIndex];

        int mult;
        string prefix;

        if (rNorm >= doubleIn && rNorm <= doubleOut * 1.01)
        {
            mult = 2;
            prefix = "D";
        }
        else if (rNorm >= tripleIn && rNorm <= tripleOut)
        {
            mult = 3;
            prefix = "T";
        }
        else
        {
            mult = 1;
            prefix = "S";
        }

        var score = baseNum * mult;
        var label = $"{prefix}{baseNum} ({score})";

        return new DartScoreResult(false, baseNum, mult, score, label);
    }
}