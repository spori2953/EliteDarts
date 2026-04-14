using OpenCvSharp;

namespace EliteDarts.CvWorker.Models;

public class CvCalibrationState
{
    public bool HasCalibration { get; set; }

    public int CameraIndex { get; set; } = 1;
    public int Width { get; set; } = 1280;
    public int Height { get; set; } = 720;

    public int BoardCx { get; set; }
    public int BoardCy { get; set; }
    public int BoardR { get; set; }

    public Mat? EmptyBaseline { get; set; }
}

public record CalibrateRequest(
    int? CameraIndex,
    int? Width,
    int? Height,
    int? WarmupFrames,
    int? WarmupSleepMs,
    double? HoughDp,
    double? HoughMinDist,
    double? HoughParam1,
    double? HoughParam2,
    int? HoughMinRadius,
    int? HoughMaxRadius,
    int? BoardCx,
    int? BoardCy,
    int? BoardR,
    bool? SaveDebugImages,
    string? DebugDir
);

public record CalibrateResponse
{
    public bool Ok { get; set; }
    public int BoardCx { get; set; }
    public int BoardCy { get; set; }
    public int BoardR { get; set; }
    public string? Message { get; set; }
}

public record VisitScanRequest(
    int? CameraIndex,
    int? Width,
    int? Height,
    int? WarmupFrames,
    int? WarmupSleepMs,
    int? EmptyTimeoutMs,
    int? EmptyThreshold,
    int? EmptyPixels,
    int? EmptyStableFrames,
    int? MotionTimeoutMs,
    int? SettleMs,
    int? Threshold,
    double? MinArea,
    int? MaxDarts,
    double? RotationDegCWFromTop,
    int? NewDartStableFrames,
    int? NewDartMinPixels,
    bool? SaveDebugImages,
    string? DebugDir
);

public record VisitScanResponse
{
    public bool Ok { get; set; }
    public int BoardCx { get; set; }
    public int BoardCy { get; set; }
    public int BoardR { get; set; }
    public List<DartDetection> Darts { get; set; } = new();
    public string? Message { get; set; }
}

public record RectDto(int X, int Y, int W, int H);

public record DartDetection
{
    public int DartNo { get; set; }
    public bool Found { get; set; }
    public double? TipX { get; set; }
    public double? TipY { get; set; }
    public double? Confidence { get; set; }
    public double? Area { get; set; }
    public RectDto? Rect { get; set; }
    public string? Message { get; set; }

    public int? BaseNumber { get; set; }
    public int? Multiplier { get; set; }
    public int? Score { get; set; }
    public string? Label { get; set; }
}