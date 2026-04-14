namespace EliteDarts.Entities
{
    public record VisitScanRequest(
        int? CameraIndex = 1,
        int? Width = 1280,
        int? Height = 720,

        int? WarmupFrames = 10,
        int? WarmupSleepMs = 5,

        int? EmptyTimeoutMs = 30000,
        int? EmptyThreshold = 35,
        int? EmptyPixels = 20000,
        int? EmptyStableFrames = 3,

        int? MotionTimeoutMs = 30000,
        int? MotionPixels = null,
        int? MotionThreshold = null,
        int? SettleMs = null,

        int? Threshold = 35,
        double? MinArea = 250,

        int? MaxDarts = 3,
        double? RotationDegCWFromTop = 0.0,

        int? NewDartStableFrames = 2,
        int? NewDartMinPixels = 500,

        bool? SaveDebugImages = true,
        string? DebugDir = "debug"
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

    public record DartDetection
    {
        public int DartNo { get; set; }
        public bool Found { get; set; }
        public double? TipX { get; set; }
        public double? TipY { get; set; }
        public double? Confidence { get; set; }
        public double? Area { get; set; }
        public int? BaseNumber { get; set; }
        public int? Multiplier { get; set; }
        public int? Score { get; set; }
        public string? Label { get; set; }
        public string? Message { get; set; }
    }
}