using OpenCvSharp;

namespace EliteDarts.CvWorker.Camera;

public interface ICameraSession
{
    void EnsureOpened(int cameraIndex, int width, int height);
    void Warmup(int frames, int sleepMs);
    Mat ReadFrameSafe();
}