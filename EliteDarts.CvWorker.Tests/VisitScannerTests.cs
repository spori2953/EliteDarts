using EliteDarts.CvWorker.Models;
using EliteDarts.CvWorker.Services;
using OpenCvSharp;
using Xunit;

namespace EliteDarts.CvWorker.Tests;

public class VisitScannerTests
{
    [Fact]
    public void Scan_ShouldReturnError_WhenNoCalibration()
    {
        var calib = new CvCalibrationState
        {
            HasCalibration = false
        };

        var fakeCamera = new FakeCameraSession(Array.Empty<Mat>());
        var scanner = new VisitScanner(fakeCamera, calib);

        var req = new VisitScanRequest(
            CameraIndex: null,
            Width: null,
            Height: null,
            WarmupFrames: 0,
            WarmupSleepMs: 0,
            EmptyTimeoutMs: 100,
            EmptyThreshold: 20,
            EmptyPixels: null,
            EmptyStableFrames: 2,
            MotionTimeoutMs: 100,
            SettleMs: 0,
            Threshold: 18,
            MinArea: 50,
            MaxDarts: 3,
            RotationDegCWFromTop: 0.0,
            NewDartStableFrames: 1,
            NewDartMinPixels: 20,
            SaveDebugImages: false,
            DebugDir: null
        );

        var result = scanner.Scan(req);

        Assert.False(result.Ok);
        Assert.Contains("Nincs kalibráció", result.Message);
    }

    [Fact]
    public void Scan_ShouldDetectOneDart_WhenFrameContainsNewObject()
    {
        using var empty = CreateEmptyBoardFrame(640, 480);
        using var withDart = empty.Clone();

        // Rajzolunk egy "fake nyilat"
        DrawFakeDart(withDart, new Point(320, 130), 40, 6);

        var calib = new CvCalibrationState
        {
            HasCalibration = true,
            CameraIndex = 0,
            Width = 640,
            Height = 480,
            BoardCx = 320,
            BoardCy = 240,
            BoardR = 170,
            EmptyBaseline = empty.Clone()
        };

        var frames = new List<Mat>
        {
            empty.Clone(),
            empty.Clone(),
            empty.Clone(),
            withDart.Clone(),
            withDart.Clone(),
            withDart.Clone(),
            withDart.Clone(),
            withDart.Clone()
        };

        var fakeCamera = new FakeCameraSession(frames);
        var scanner = new VisitScanner(fakeCamera, calib);

        var req = new VisitScanRequest(
            CameraIndex: 0,
            Width: 640,
            Height: 480,
            WarmupFrames: 0,
            WarmupSleepMs: 0,
            EmptyTimeoutMs: 300,
            EmptyThreshold: 20,
            EmptyPixels: null,
            EmptyStableFrames: 2,
            MotionTimeoutMs: 500,
            SettleMs: 0,
            Threshold: 10,
            MinArea: 20,
            MaxDarts: 1,
            RotationDegCWFromTop: 0.0,
            NewDartStableFrames: 1,
            NewDartMinPixels: 10,
            SaveDebugImages: false,
            DebugDir: null
        );

        var result = scanner.Scan(req);

        Assert.True(result.Ok);
        Assert.NotEmpty(result.Darts);
        Assert.True(result.Darts.Count >= 1);

        var dart = result.Darts[0];

        Assert.True(dart.Found);
        Assert.NotNull(dart.Score);
    }

    [Fact]
    public void Scan_ShouldDetectTwoDarts_InOneVisit()
    {
        using var empty = CreateEmptyBoardFrame(640, 480);

        using var withDart1 = empty.Clone();
        Cv2.Circle(withDart1, new Point(320, 100), 10, Scalar.White, -1);

        using var withDart1And2 = withDart1.Clone();
        Cv2.Circle(withDart1And2, new Point(380, 180), 10, Scalar.White, -1);

        var calib = new CvCalibrationState
        {
            HasCalibration = true,
            CameraIndex = 0,
            Width = 640,
            Height = 480,
            BoardCx = 320,
            BoardCy = 240,
            BoardR = 170,
            EmptyBaseline = empty.Clone()
        };

        var frames = new List<Mat>
    {
        // stabil üres baseline
        empty.Clone(),
        empty.Clone(),
        empty.Clone(),

        // első nyíl megjelenik
        withDart1.Clone(),
        withDart1.Clone(),
        withDart1.Clone(),

        // második nyíl megjelenik az első mellé
        withDart1And2.Clone(),
        withDart1And2.Clone(),
        withDart1And2.Clone(),
        withDart1And2.Clone()
    };

        var fakeCamera = new FakeCameraSession(frames);
        var scanner = new VisitScanner(fakeCamera, calib);

        var req = new VisitScanRequest(
            CameraIndex: 0,
            Width: 640,
            Height: 480,
            WarmupFrames: 0,
            WarmupSleepMs: 0,
            EmptyTimeoutMs: 300,
            EmptyThreshold: 20,
            EmptyPixels: null,
            EmptyStableFrames: 2,
            MotionTimeoutMs: 500,
            SettleMs: 0,
            Threshold: 10,
            MinArea: 20,
            MaxDarts: 2,
            RotationDegCWFromTop: 0.0,
            NewDartStableFrames: 1,
            NewDartMinPixels: 10,
            SaveDebugImages: false,
            DebugDir: null
        );

        var result = scanner.Scan(req);

        Assert.True(result.Ok);
        Assert.Equal(2, result.Darts.Count);
        Assert.All(result.Darts, d => Assert.True(d.Found));
    }

    [Fact]
    public void Scan_ShouldCalculateExactSingle20Score_ForDetectedDart()
    {
        using var empty = CreateEmptyBoardFrame(640, 480);
        using var withDart = empty.Clone();

        // Single 20 tartomány: a tábla tetején, de ne a tripla gyűrűben
        DrawFakeDart(withDart, new Point(320, 150), 40, 6);

        var calib = new CvCalibrationState
        {
            HasCalibration = true,
            CameraIndex = 0,
            Width = 640,
            Height = 480,
            BoardCx = 320,
            BoardCy = 240,
            BoardR = 170,
            EmptyBaseline = empty.Clone()
        };

        var frames = new List<Mat>
    {
        empty.Clone(),
        empty.Clone(),
        empty.Clone(),
        withDart.Clone(),
        withDart.Clone(),
        withDart.Clone(),
        withDart.Clone(),
        withDart.Clone()
    };

        var fakeCamera = new FakeCameraSession(frames);
        var scanner = new VisitScanner(fakeCamera, calib);

        var req = new VisitScanRequest(
            CameraIndex: 0,
            Width: 640,
            Height: 480,
            WarmupFrames: 0,
            WarmupSleepMs: 0,
            EmptyTimeoutMs: 300,
            EmptyThreshold: 20,
            EmptyPixels: null,
            EmptyStableFrames: 2,
            MotionTimeoutMs: 500,
            SettleMs: 0,
            Threshold: 10,
            MinArea: 20,
            MaxDarts: 1,
            RotationDegCWFromTop: 0.0,
            NewDartStableFrames: 1,
            NewDartMinPixels: 10,
            SaveDebugImages: false,
            DebugDir: null
        );

        var result = scanner.Scan(req);

        Assert.True(result.Ok);
        Assert.Single(result.Darts);

        var dart = result.Darts[0];

        Assert.True(dart.Found);
        Assert.Equal(20, dart.BaseNumber);
        Assert.Equal(1, dart.Multiplier);
        Assert.Equal(20, dart.Score);
        Assert.Equal("S20 (20)", dart.Label);
    }

    [Fact]
    public void Scan_ShouldCalculateAccuracy_ForMultipleDarts()
    {
        using var empty = CreateEmptyBoardFrame(640, 480);

        var testCases = new List<(Point tip, int expectedScore)>
    {
        (new Point(320, 160), 20), // S20
        (new Point(320, 133), 60), // T20
        (new Point(321, 160), 20)  // S20 kis eltéréssel
    };

        int correct = 0;
        int total = testCases.Count;

        foreach (var test in testCases)
        {
            using var withDart = empty.Clone();

            DrawFakeDart(withDart, test.tip, 60, 3);

            var calib = new CvCalibrationState
            {
                HasCalibration = true,
                CameraIndex = 0,
                Width = 640,
                Height = 480,
                BoardCx = 320,
                BoardCy = 240,
                BoardR = 170,
                EmptyBaseline = empty.Clone()
            };

            var frames = new List<Mat>
        {
            empty.Clone(),
            empty.Clone(),
            empty.Clone(),
            withDart.Clone(),
            withDart.Clone(),
            withDart.Clone(),
            withDart.Clone(),
            withDart.Clone()
        };

            var fakeCamera = new FakeCameraSession(frames);
            var scanner = new VisitScanner(fakeCamera, calib);

            var req = new VisitScanRequest(
                0, 640, 480,
                0, 0,
                300, 20, null, 2,
                500, 0,
                10, 20,
                1,
                0.0,
                1, 10,
                false, null
            );

            var result = scanner.Scan(req);

            if (result.Darts.Count > 0)
            {
                var dart = result.Darts[0];

                bool isCorrect =
                    (test.expectedScore == 20 && dart.BaseNumber == 20 && dart.Multiplier == 1) ||
                    (test.expectedScore == 60 && dart.BaseNumber == 20 && dart.Multiplier == 3);

                if (isCorrect)
                {
                    correct++;
                }
            }
        }

        double accuracy = (double)correct / total;

        Console.WriteLine($"Accuracy: {accuracy * 100:F2}% ({correct}/{total})");

        Assert.True(accuracy >= 0.7);
    }

    [Fact]
    public void Scan_ShouldCalculateAccuracy_For100Darts()
    {
        using var empty = CreateEmptyBoardFrame(640, 480);

        var testCases = Create100DartTestCases();

        int correct = 0;
        int total = testCases.Count;

        foreach (var test in testCases)
        {
            using var withDart = empty.Clone();

            DrawFakeDart(withDart, test.Tip, 40, 6);

            var calib = new CvCalibrationState
            {
                HasCalibration = true,
                CameraIndex = 0,
                Width = 640,
                Height = 480,
                BoardCx = 320,
                BoardCy = 240,
                BoardR = 170,
                EmptyBaseline = empty.Clone()
            };

            var frames = new List<Mat>
        {
            empty.Clone(),
            empty.Clone(),
            empty.Clone(),
            withDart.Clone(),
            withDart.Clone(),
            withDart.Clone(),
            withDart.Clone(),
            withDart.Clone()
        };

            var fakeCamera = new FakeCameraSession(frames);
            var scanner = new VisitScanner(fakeCamera, calib);

            var req = new VisitScanRequest(
                CameraIndex: 0,
                Width: 640,
                Height: 480,
                WarmupFrames: 0,
                WarmupSleepMs: 0,
                EmptyTimeoutMs: 300,
                EmptyThreshold: 20,
                EmptyPixels: null,
                EmptyStableFrames: 2,
                MotionTimeoutMs: 500,
                SettleMs: 0,
                Threshold: 10,
                MinArea: 20,
                MaxDarts: 1,
                RotationDegCWFromTop: 0.0,
                NewDartStableFrames: 1,
                NewDartMinPixels: 10,
                SaveDebugImages: false,
                DebugDir: null
            );

            var result = scanner.Scan(req);

            if (result.Darts.Count > 0)
            {
                var dart = result.Darts[0];

                bool isCorrect =
                    (test.ExpectedScore == 20 && dart.BaseNumber == 20 && dart.Multiplier == 1) ||
                    (test.ExpectedScore == 60 && dart.BaseNumber == 20 && dart.Multiplier == 3);
                    

                if (isCorrect)
                {
                    correct++;
                }
            }
        }

        double accuracy = (double)correct / total;

        Console.WriteLine($"100-dart accuracy: {accuracy * 100:F2}% ({correct}/{total})");

        Assert.True(total == 100);
        Assert.True(accuracy >= 0.50);
    }

    [Fact]
    public void Scan_ShouldCalculateAccuracy_WithNoise()
    {
        using var empty = CreateEmptyBoardFrame(640, 480);

        var rnd = new Random(42);

        int correct = 0;
        int total = 100;

        for (int i = 0; i < total; i++)
        {
            // 50-50 eséllyel S20 vagy T20
            bool isTriple = rnd.NextDouble() < 0.5;

            Point center;
            int expectedScore;

            if (isTriple)
            {
                center = new Point(320, 133);
                expectedScore = 60;
            }
            else
            {
                center = new Point(320, 150); 
                expectedScore = 20;
            }

            // SZÓRÁS ±3 pixel
            int dx = rnd.Next(-3, 4);
            int dy = rnd.Next(-3, 4);

            var noisyPoint = new Point(center.X + dx, center.Y + dy);

            using var withDart = empty.Clone();
            DrawFakeDart(withDart, noisyPoint, 40, 6);

            var calib = new CvCalibrationState
            {
                HasCalibration = true,
                CameraIndex = 0,
                Width = 640,
                Height = 480,
                BoardCx = 320,
                BoardCy = 240,
                BoardR = 170,
                EmptyBaseline = empty.Clone()
            };

            var frames = new List<Mat>
        {
            empty.Clone(),
            empty.Clone(),
            empty.Clone(),
            withDart.Clone(),
            withDart.Clone(),
            withDart.Clone(),
            withDart.Clone(),
            withDart.Clone()
        };

            var fakeCamera = new FakeCameraSession(frames);
            var scanner = new VisitScanner(fakeCamera, calib);

            var req = new VisitScanRequest(
                0, 640, 480,
                0, 0,
                300, 20, null, 2,
                500, 0,
                10, 20,
                1,
                0.0,
                1, 10,
                false, null
            );

            var result = scanner.Scan(req);

            if (result.Darts.Count > 0)
            {
                var dart = result.Darts[0];

                bool isCorrect =
                    (expectedScore == 20 && dart.BaseNumber == 20 && dart.Multiplier == 1) ||
                    (expectedScore == 60 && dart.BaseNumber == 20 && dart.Multiplier == 3);

                if (isCorrect)
                {
                    correct++;
                }
            }
        }

        double accuracy = (double)correct / total;

        Console.WriteLine($"Noise accuracy: {accuracy * 100:F2}% ({correct}/{total})");

        Assert.True(accuracy >= 0.70);
    }

    private static Mat CreateEmptyBoardFrame(int width, int height)
    {
        var img = new Mat(height, width, MatType.CV_8UC3, Scalar.Black);

        // egyszerű kör (tábla)
        Cv2.Circle(img, new Point(width / 2, height / 2), 170, new Scalar(180, 180, 180), 2);

        return img;
    }

    private static void DrawFakeDart(Mat img, Point tip, int length, int thickness)
    {
        var tail = new Point(tip.X, tip.Y - length);

        Cv2.Line(img, tail, tip, Scalar.White, thickness);

        var wingLeft = new Point(tip.X - 4, tip.Y - 8);
        var wingRight = new Point(tip.X + 4, tip.Y - 8);

        Cv2.FillConvexPoly(img, new[] { tip, wingLeft, wingRight }, Scalar.White);
    }
    private record DartAccuracyCase(Point Tip, int ExpectedScore);

    private static List<DartAccuracyCase> Create100DartTestCases()
    {
        var cases = new List<DartAccuracyCase>();

        var single20Points = new List<Point>
    {
        new Point(319, 148),
        new Point(320, 150),
        new Point(321, 152),
        new Point(320, 149),
        new Point(321, 151)
    };

        var triple20Points = new List<Point>
    {
        new Point(319, 131),
        new Point(320, 133),
        new Point(321, 135),
        new Point(320, 134),
        new Point(321, 132)
    };

        for (int i = 0; i < 50; i++)
        {
            cases.Add(new DartAccuracyCase(
                single20Points[i % single20Points.Count],
                20));
        }

        for (int i = 0; i < 50; i++)
        {
            cases.Add(new DartAccuracyCase(
                triple20Points[i % triple20Points.Count],
                60));
        }

        return cases;
    }
}