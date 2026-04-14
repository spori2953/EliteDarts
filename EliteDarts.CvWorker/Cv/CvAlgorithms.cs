using EliteDarts.CvWorker.Camera;
using EliteDarts.CvWorker.Models;
using OpenCvSharp;
using System.Diagnostics;

namespace EliteDarts.CvWorker.Services;

public static class CvAlgorithms
{
    public static int GetPlayRadius(int boardR) => Math.Max(40, (int)(boardR * 0.82));

    public static double Dist2(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }

    public static (Point A, Point B) FindFarthestPair(Point[] pts)
    {
        var bestA = pts[0];
        var bestB = pts[0];
        double best = -1;

        for (int i = 0; i < pts.Length; i++)
        {
            for (int j = i + 1; j < pts.Length; j++)
            {
                var d = Dist2(pts[i], pts[j]);
                if (d > best)
                {
                    best = d;
                    bestA = pts[i];
                    bestB = pts[j];
                }
            }
        }

        return (bestA, bestB);
    }

    public static DartDetection DetectTipByDiff(
        Mat before,
        Mat after,
        int boardCx,
        int boardCy,
        int boardR,
        int threshold,
        double minArea,
        bool saveDebug,
        string debugDir,
        string tsPrefix)
    {
        var playR = GetPlayRadius(boardR);

        using var diff = new Mat();
        Cv2.Absdiff(after, before, diff);
        if (saveDebug)
            Cv2.ImWrite(Path.Combine(debugDir, $"{tsPrefix}_diff.jpg"), diff);

        using var gray = new Mat();
        Cv2.CvtColor(diff, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.GaussianBlur(gray, gray, new Size(5, 5), 0);

        using var bin = new Mat();
        Cv2.Threshold(gray, bin, threshold, 255, ThresholdTypes.Binary);

        using (var mask = new Mat(bin.Rows, bin.Cols, MatType.CV_8UC1, Scalar.Black))
        {
            Cv2.Circle(mask, new Point(boardCx, boardCy), playR, Scalar.White, thickness: -1);
            Cv2.BitwiseAnd(bin, mask, bin);
        }

        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(5, 5));
        Cv2.MorphologyEx(bin, bin, MorphTypes.Close, kernel, iterations: 2);
        Cv2.MorphologyEx(bin, bin, MorphTypes.Open, kernel, iterations: 1);

        if (saveDebug)
            Cv2.ImWrite(Path.Combine(debugDir, $"{tsPrefix}_bin.jpg"), bin);

        Cv2.FindContours(bin, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        if (contours.Length == 0)
        {
            return new DartDetection
            {
                Found = false,
                Message = "Nincs kontúr."
            };
        }

        var candidates = contours
            .Select(c =>
            {
                var area = Cv2.ContourArea(c);
                var rect = Cv2.BoundingRect(c);
                var aspect = rect.Width > rect.Height
                    ? (double)rect.Width / Math.Max(rect.Height, 1)
                    : (double)rect.Height / Math.Max(rect.Width, 1);

                return new { C = c, Area = area, Rect = rect, Aspect = aspect };
            })
            .Where(x => x.Area >= minArea)
            .OrderByDescending(x => x.Area * Math.Max(1.0, x.Aspect))
            .ToList();

        if (candidates.Count == 0)
        {
            return new DartDetection
            {
                Found = false,
                Message = "Nincs megfelelő kontúr."
            };
        }

        var best = candidates.First();
        var rect = best.Rect;

        var (pA, pB) = FindFarthestPair(best.C);
        var dA = Dist2(pA, new Point(boardCx, boardCy));
        var dB = Dist2(pB, new Point(boardCx, boardCy));
        var tip = dA < dB ? pA : pB;

        var conf = Math.Clamp(best.Area / 2000.0, 0.1, 1.0);

        if (saveDebug)
        {
            using var vis = after.Clone();
            Cv2.Circle(vis, new Point(boardCx, boardCy), playR, Scalar.Cyan, 2);
            Cv2.Rectangle(vis, rect, Scalar.Yellow, 2);
            Cv2.Circle(vis, tip, 6, Scalar.Red, -1);
            Cv2.Circle(vis, new Point(boardCx, boardCy), 5, Scalar.LimeGreen, -1);
            Cv2.ImWrite(Path.Combine(debugDir, $"{tsPrefix}_vis.jpg"), vis);
        }

        return new DartDetection
        {
            Found = true,
            TipX = tip.X,
            TipY = tip.Y,
            Confidence = conf,
            Area = best.Area,
            Rect = new RectDto(rect.X, rect.Y, rect.Width, rect.Height),
            Message = "OK"
        };
    }

    public static bool WaitForNewDart(
        ICameraSession camera,
        Mat baseline,
        int boardCx,
        int boardCy,
        int boardR,
        int timeoutMs,
        int threshold,
        int minChangedPixels,
        int stableFrames,
        int settleMs,
        out Mat afterFrame)
    {
        afterFrame = new Mat();

        var playR = GetPlayRadius(boardR);

        using var diff = new Mat();
        using var gray = new Mat();
        using var bin = new Mat();
        using var mask = new Mat(baseline.Rows, baseline.Cols, MatType.CV_8UC1, Scalar.Black);
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(5, 5));

        Cv2.Circle(mask, new Point(boardCx, boardCy), playR, Scalar.White, -1);

        int okCount = 0;
        var sw = Stopwatch.StartNew();

        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            using var curr = camera.ReadFrameSafe();
            if (curr.Empty()) continue;

            Cv2.Absdiff(curr, baseline, diff);
            Cv2.CvtColor(diff, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.GaussianBlur(gray, gray, new Size(5, 5), 0);
            Cv2.Threshold(gray, bin, threshold, 255, ThresholdTypes.Binary);

            Cv2.BitwiseAnd(bin, mask, bin);
            Cv2.MorphologyEx(bin, bin, MorphTypes.Close, kernel, iterations: 2);
            Cv2.MorphologyEx(bin, bin, MorphTypes.Open, kernel, iterations: 1);

            var changed = Cv2.CountNonZero(bin);

            if (changed >= minChangedPixels)
            {
                okCount++;
                if (okCount >= stableFrames)
                {
                    Thread.Sleep(settleMs);
                    using var settled = camera.ReadFrameSafe();
                    settled.CopyTo(afterFrame);
                    return true;
                }
            }
            else
            {
                okCount = 0;
            }

            Thread.Sleep(40);
        }

        return false;
    }

    public static bool WaitForStableBoard(
        ICameraSession camera,
        int boardCx,
        int boardCy,
        int boardR,
        int timeoutMs,
        int thr,
        int stableFramesNeeded,
        out Mat stableFrame)
    {
        stableFrame = new Mat();

        var playR = GetPlayRadius(boardR);

        using var prev = new Mat();
        using var curr = new Mat();
        using var diff = new Mat();
        using var gray = new Mat();
        using var bin = new Mat();
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(5, 5));

        using var first = camera.ReadFrameSafe();
        if (first.Empty()) return false;

        using var realMask = new Mat(first.Rows, first.Cols, MatType.CV_8UC1, Scalar.Black);
        Cv2.Circle(realMask, new Point(boardCx, boardCy), playR, Scalar.White, -1);

        first.CopyTo(prev);

        int okCount = 0;
        var sw = Stopwatch.StartNew();

        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            using var live = camera.ReadFrameSafe();
            if (live.Empty()) continue;

            live.CopyTo(curr);

            Cv2.Absdiff(curr, prev, diff);
            Cv2.CvtColor(diff, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.GaussianBlur(gray, gray, new Size(5, 5), 0);
            Cv2.Threshold(gray, bin, thr, 255, ThresholdTypes.Binary);

            Cv2.BitwiseAnd(bin, realMask, bin);
            Cv2.MorphologyEx(bin, bin, MorphTypes.Close, kernel, iterations: 1);
            Cv2.MorphologyEx(bin, bin, MorphTypes.Open, kernel, iterations: 1);

            var changed = Cv2.CountNonZero(bin);

            if (changed <= 300)
            {
                okCount++;
                if (okCount >= stableFramesNeeded)
                {
                    curr.CopyTo(stableFrame);
                    return true;
                }
            }
            else
            {
                okCount = 0;
            }

            curr.CopyTo(prev);
            Thread.Sleep(40);
        }

        return false;
    }
}