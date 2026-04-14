using EliteDarts.CvWorker.Camera;
using EliteDarts.CvWorker.Models;
using OpenCvSharp;

namespace EliteDarts.CvWorker.Services;

public class VisitScanner
{
    private readonly ICameraSession _camera;
    private readonly CvCalibrationState _calib;

    public VisitScanner(ICameraSession camera, CvCalibrationState calib)
    {
        _camera = camera;
        _calib = calib;
    }

    public VisitScanResponse Scan(VisitScanRequest req)
    {
        if (!_calib.HasCalibration || _calib.EmptyBaseline is null)
        {
            return new VisitScanResponse
            {
                Ok = false,
                Message = "Nincs kalibráció. Előbb hívd meg: POST /calibrate/auto (üres táblán)."
            };
        }

        var cameraIndex = req.CameraIndex ?? _calib.CameraIndex;
        var w = req.Width ?? _calib.Width;
        var h = req.Height ?? _calib.Height;

        _camera.EnsureOpened(cameraIndex, w, h);
        _camera.Warmup(req.WarmupFrames ?? 10, req.WarmupSleepMs ?? 5);

        var emptyTimeout = req.EmptyTimeoutMs ?? 30000;
        var emptyThr = req.EmptyThreshold ?? 20;
        var emptyStableFrames = req.EmptyStableFrames ?? 5;

        if (!CvAlgorithms.WaitForStableBoard(
                _camera,
                _calib.BoardCx, _calib.BoardCy, _calib.BoardR,
                timeoutMs: emptyTimeout,
                thr: emptyThr,
                stableFramesNeeded: emptyStableFrames,
                out var baseline))
        {
            return new VisitScanResponse
            {
                Ok = false,
                BoardCx = _calib.BoardCx,
                BoardCy = _calib.BoardCy,
                BoardR = _calib.BoardR,
                Message = "Nem sikerült stabil, üres baseline képet készíteni a visit elején."
            };
        }

        using (baseline)
        {
            _calib.EmptyBaseline?.Dispose();
            _calib.EmptyBaseline = baseline.Clone();

            var results = new List<DartDetection>();
            var maxDarts = Math.Clamp(req.MaxDarts ?? 3, 1, 3);

            var newDartTimeout = req.MotionTimeoutMs ?? 20000;
            var thr = req.Threshold ?? 18;
            var minArea = req.MinArea ?? 120;
            var stableFrames = req.NewDartStableFrames ?? 2;
            var minChangedPixels = req.NewDartMinPixels ?? 220;
            var settleMs = req.SettleMs ?? 450;

            using var curBaseline = baseline.Clone();

            for (int dartNo = 1; dartNo <= maxDarts; dartNo++)
            {
                if (!CvAlgorithms.WaitForNewDart(
                        _camera,
                        curBaseline,
                        _calib.BoardCx, _calib.BoardCy, _calib.BoardR,
                        timeoutMs: newDartTimeout,
                        threshold: thr,
                        minChangedPixels: minChangedPixels,
                        stableFrames: stableFrames,
                        settleMs: settleMs,
                        out var after))
                {
                    break;
                }

                using (after)
                {
                    var det = CvAlgorithms.DetectTipByDiff(
                        before: curBaseline,
                        after: after,
                        boardCx: _calib.BoardCx,
                        boardCy: _calib.BoardCy,
                        boardR: _calib.BoardR,
                        threshold: thr,
                        minArea: minArea,
                        saveDebug: false,
                        debugDir: "debug",
                        tsPrefix: "test"
                    );

                    det.DartNo = dartNo;

                    if (det.Found && det.TipX.HasValue && det.TipY.HasValue)
                    {
                        var sc = DartScoring.CalculateDartScore(
                            det.TipX.Value,
                            det.TipY.Value,
                            _calib.BoardCx,
                            _calib.BoardCy,
                            _calib.BoardR,
                            rotationDegCWFromTop: req.RotationDegCWFromTop ?? 0.0
                        );

                        det.BaseNumber = sc.BaseNumber;
                        det.Multiplier = sc.Multiplier;
                        det.Score = sc.Score;
                        det.Label = sc.Label;
                    }

                    results.Add(det);
                    after.CopyTo(curBaseline);
                }
            }

            return new VisitScanResponse
            {
                Ok = true,
                BoardCx = _calib.BoardCx,
                BoardCy = _calib.BoardCy,
                BoardR = _calib.BoardR,
                Darts = results,
                Message = results.Count == 0
                    ? "Nem találtam dobást."
                    : $"OK ({results.Count} dobás)."
            };
        }
    }
}