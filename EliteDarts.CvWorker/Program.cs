using EliteDarts.CvWorker.Camera;
using EliteDarts.CvWorker.Models;
using EliteDarts.CvWorker.Services;
using OpenCvSharp;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5055");
var app = builder.Build();

app.MapGet("/", () => "EliteDarts CvWorker OK");

CvCalibrationState Calib = new();
CameraSession Camera = new();

app.MapPost("/calibrate/auto", (CalibrateRequest req) =>
{
    var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
    var save = req.SaveDebugImages == true;
    var debugDir = req.DebugDir ?? "debug";

    EnsureDir(debugDir);

    try
    {
        Camera.EnsureOpened(
            req.CameraIndex ?? 1,
            req.Width ?? 1280,
            req.Height ?? 720);

        Camera.Warmup(req.WarmupFrames ?? 40, req.WarmupSleepMs ?? 10);

        using var frame = Camera.ReadFrameSafe();
        if (frame.Empty())
            return Results.BadRequest(new { error = "Nem sikerült képet olvasni a kalibrációhoz." });

        if (save)
            Cv2.ImWrite(PathCombine(debugDir, $"{ts}_calib_frame.jpg"), frame);

        var (found, cx, cy, r) = TryFindBoardCircleByEllipse(frame);

        if (found && !CircleMostlyInside(cx, cy, r, frame.Cols, frame.Rows, 0.9))
            found = false;

        if (found)
        {
            var refined = TryRefineCenterByBull(frame, cx, cy, r);
            if (refined.found)
            {
                cx = refined.cx;
                cy = refined.cy;
            }
        }

        if (!found)
        {
            if (req.BoardCx is null || req.BoardCy is null || req.BoardR is null)
            {
                return Results.BadRequest(new
                {
                    error = "Nem találtam táblát. Adj meg BoardCx/BoardCy/BoardR paramétert kézzel."
                });
            }

            cx = req.BoardCx.Value;
            cy = req.BoardCy.Value;
            r = req.BoardR.Value;
        }

        Calib.HasCalibration = true;
        Calib.CameraIndex = req.CameraIndex ?? 1;
        Calib.Width = req.Width ?? 1280;
        Calib.Height = req.Height ?? 720;
        Calib.BoardCx = cx;
        Calib.BoardCy = cy;
        Calib.BoardR = r;

        Calib.EmptyBaseline?.Dispose();
        Calib.EmptyBaseline = frame.Clone();

        if (save)
        {
            using var vis = frame.Clone();
            Cv2.Circle(vis, new Point(cx, cy), r, Scalar.LimeGreen, 3);
            Cv2.Circle(vis, new Point(cx, cy), 6, Scalar.Red, -1);
            Cv2.Circle(vis, new Point(cx, cy), CvAlgorithms.GetPlayRadius(r), Scalar.Yellow, 2);
            Cv2.ImWrite(PathCombine(debugDir, $"{ts}_calib_vis.jpg"), vis);
        }

        return Results.Ok(new CalibrateResponse
        {
            Ok = true,
            BoardCx = cx,
            BoardCy = cy,
            BoardR = r,
            Message = "OK"
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = $"Kalibrációs hiba: {ex.Message}" });
    }
});

app.MapPost("/visit/scan", (VisitScanRequest req) =>
{
    try
    {
        var scanner = new VisitScanner(Camera, Calib);
        var result = scanner.Scan(req);

        if (!result.Ok && result.Message != null && result.Message.StartsWith("Nincs kalibráció"))
            return Results.BadRequest(new { error = result.Message });

        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = $"Visit scan hiba: {ex.Message}" });
    }
});

app.Run();


// ----------------- CV HELPERS -----------------


static (bool found, int cx, int cy, int r) TryFindBoardCircleByEllipse(Mat frame)
{
    using var gray = new Mat();
    Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
    Cv2.GaussianBlur(gray, gray, new Size(7, 7), 1.5);

    using var edges = new Mat();
    Cv2.Canny(gray, edges, 60, 160);

    using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(7, 7));
    Cv2.Dilate(edges, edges, kernel, iterations: 1);
    Cv2.MorphologyEx(edges, edges, MorphTypes.Close, kernel, iterations: 2);

    Cv2.FindContours(edges, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
    if (contours.Length == 0)
        return (false, 0, 0, 0);

    var candidates = contours
        .Where(c => c.Length >= 30)
        .Select(c => new { C = c, Area = Cv2.ContourArea(c) })
        .OrderByDescending(x => x.Area)
        .Take(15);

    foreach (var cand in candidates)
    {
        var ellipse = Cv2.FitEllipse(cand.C);

        var a = ellipse.Size.Width / 2.0;
        var b = ellipse.Size.Height / 2.0;

        if (a < 120 || b < 120) continue;
        if (a > 1200 || b > 1200) continue;

        var ratio = a > b ? a / b : b / a;
        if (ratio > 1.45) continue;

        var cx = (int)Math.Round(ellipse.Center.X);
        var cy = (int)Math.Round(ellipse.Center.Y);
        if (cx < 0 || cy < 0 || cx >= frame.Cols || cy >= frame.Rows) continue;

        var r = (int)Math.Round((a + b) / 2.0);
        return (true, cx, cy, r);
    }

    return (false, 0, 0, 0);
}

static (bool found, int cx, int cy) TryRefineCenterByBull(Mat frame, int approxCx, int approxCy, int boardR)
{
    int roiHalf = Math.Max(50, boardR / 6);

    int x1 = Math.Max(0, approxCx - roiHalf);
    int y1 = Math.Max(0, approxCy - roiHalf);
    int x2 = Math.Min(frame.Cols, approxCx + roiHalf);
    int y2 = Math.Min(frame.Rows, approxCy + roiHalf);

    int w = x2 - x1;
    int h = y2 - y1;

    if (w < 40 || h < 40)
        return (false, approxCx, approxCy);

    using var roi = new Mat(frame, new Rect(x1, y1, w, h));
    using var gray = new Mat();
    Cv2.CvtColor(roi, gray, ColorConversionCodes.BGR2GRAY);
    Cv2.GaussianBlur(gray, gray, new Size(7, 7), 1.5);

    int minR = Math.Max(8, boardR / 35);
    int maxR = Math.Max(25, boardR / 8);

    var circles = Cv2.HoughCircles(
        gray,
        HoughModes.Gradient,
        dp: 1.2,
        minDist: 20,
        param1: 100,
        param2: 18,
        minRadius: minR,
        maxRadius: maxR
    );

    if (circles == null || circles.Length == 0)
        return (false, approxCx, approxCy);

    var best = circles
        .OrderBy(c =>
        {
            var dx = (x1 + c.Center.X) - approxCx;
            var dy = (y1 + c.Center.Y) - approxCy;
            return dx * dx + dy * dy;
        })
        .First();

    return (true, (int)(x1 + best.Center.X), (int)(y1 + best.Center.Y));
}

static bool CircleMostlyInside(int cx, int cy, int r, int w, int h, double margin = 0.9)
{
    var rr = (int)(r * margin);
    return (cx - rr) >= 0 && (cy - rr) >= 0 && (cx + rr) < w && (cy + rr) < h;
}


static void EnsureDir(string dir)
{
    if (!Directory.Exists(dir))
        Directory.CreateDirectory(dir);
}

static string PathCombine(string? dir, string file)
{
    dir ??= "debug";
    return Path.Combine(dir, file);
}


// ----------------- SCORING -----------------

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


// ----------------- CAMERA SESSION -----------------

sealed class CameraSession : ICameraSession
{
    private VideoCapture? _capture;
    private readonly object _sync = new();

    public int CameraIndex { get; private set; } = 1;
    public int Width { get; private set; } = 1280;
    public int Height { get; private set; } = 720;

    public void EnsureOpened(int cameraIndex, int width, int height)
    {
        lock (_sync)
        {
            if (_capture is not null &&
                _capture.IsOpened() &&
                CameraIndex == cameraIndex &&
                Width == width &&
                Height == height)
            {
                return;
            }

            CloseInternal();

            CameraIndex = cameraIndex;
            Width = width;
            Height = height;

            _capture = new VideoCapture(cameraIndex);
            if (!_capture.IsOpened())
                throw new Exception($"Nem nyitható a kamera (index={cameraIndex}).");

            _capture.Set(VideoCaptureProperties.FrameWidth, width);
            _capture.Set(VideoCaptureProperties.FrameHeight, height);
        }
    }

    public void Warmup(int frames, int sleepMs)
    {
        lock (_sync)
        {
            EnsureOpenInsideLock();

            using var tmp = new Mat();
            for (int i = 0; i < frames; i++)
            {
                _capture!.Read(tmp);
                Thread.Sleep(sleepMs);
            }
        }
    }

    public Mat ReadFrameSafe()
    {
        lock (_sync)
        {
            EnsureOpenInsideLock();

            var frame = new Mat();
            _capture!.Read(frame);

            if (!frame.Empty())
                return frame;

            frame.Dispose();

            ReopenInsideLock();

            var retry = new Mat();
            _capture!.Read(retry);

            if (retry.Empty())
            {
                retry.Dispose();
                throw new Exception("Nem sikerült frame-et olvasni a kamerából (újranyitás után sem).");
            }

            return retry;
        }
    }

    private void EnsureOpenInsideLock()
    {
        if (_capture is null || !_capture.IsOpened())
        {
            _capture = new VideoCapture(CameraIndex);
            if (!_capture.IsOpened())
                throw new Exception($"Nem nyitható a kamera (index={CameraIndex}).");

            _capture.Set(VideoCaptureProperties.FrameWidth, Width);
            _capture.Set(VideoCaptureProperties.FrameHeight, Height);
        }
    }

    private void ReopenInsideLock()
    {
        CloseInternal();

        _capture = new VideoCapture(CameraIndex);
        if (!_capture.IsOpened())
            throw new Exception($"Nem nyitható újra a kamera (index={CameraIndex}).");

        _capture.Set(VideoCaptureProperties.FrameWidth, Width);
        _capture.Set(VideoCaptureProperties.FrameHeight, Height);

        using var tmp = new Mat();
        for (int i = 0; i < 15; i++)
        {
            _capture.Read(tmp);
            Thread.Sleep(10);
        }
    }

    private void CloseInternal()
    {
        if (_capture is not null)
        {
            try { _capture.Release(); } catch { }
            try { _capture.Dispose(); } catch { }
            _capture = null;
        }
    }
}
