using EliteDarts.CvWorker.Camera;
using OpenCvSharp;

namespace EliteDarts.CvWorker.Tests;

public class FakeCameraSession : ICameraSession
{
    private readonly Queue<Mat> _frames;

    public FakeCameraSession(IEnumerable<Mat> frames)
    {
        _frames = new Queue<Mat>(frames.Select(f => f.Clone()));
    }

    public void EnsureOpened(int cameraIndex, int width, int height)
    {
    }

    public void Warmup(int frames, int sleepMs)
    {
    }

    public Mat ReadFrameSafe()
    {
        if (_frames.Count == 0)
            throw new InvalidOperationException("Nincs több fake frame.");

        return _frames.Dequeue().Clone();
    }
}