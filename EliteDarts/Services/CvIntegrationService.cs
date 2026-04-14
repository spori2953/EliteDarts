using EliteDarts.Entities;
using System.Net.Http.Json;

namespace EliteDarts.Services
{
    public class CvIntegrationService
    {
        private readonly HttpClient _httpClient;

        public CvIntegrationService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("http://localhost:5055/");
            _httpClient.Timeout = TimeSpan.FromMinutes(2);
        }

        public async Task<VisitScanResponse?> ScanVisitAsync()
        {
            try
            {
                var request = new VisitScanRequest(
                    CameraIndex: 1,
                    Width: 1280,
                    Height: 720,

                    WarmupFrames: 10,
                    WarmupSleepMs: 5,

                    EmptyTimeoutMs: 30000,
                    EmptyThreshold: 35,
                    EmptyPixels: 20000,
                    EmptyStableFrames: 3,

                    MotionTimeoutMs: 30000,

                    Threshold: 35,
                    MinArea: 250,

                    MaxDarts: 3,
                    RotationDegCWFromTop: 0.0,

                    NewDartStableFrames: 2,
                    NewDartMinPixels: 500,

                    SaveDebugImages: true,
                    DebugDir: "debug"
                );

                var response = await _httpClient.PostAsJsonAsync("visit/scan", request);

                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadFromJsonAsync<VisitScanResponse>();

                return new VisitScanResponse
                {
                    Ok = false,
                    Message = $"A CV worker hibás státuszkódot adott vissza: {(int)response.StatusCode}"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hiba a CV hívásakor: {ex.Message}");
                return new VisitScanResponse
                {
                    Ok = false,
                    Message = "Nem sikerült elérni a kamerát."
                };
            }
        }
    }
}