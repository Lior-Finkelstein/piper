using System.Text;
using Piper.App;
using Piper.Core.Http;

internal static class UpdateServiceTests
{
    public static Task RunAsync(TestRunner runner) => runner.RunAsync("release update metadata", () =>
    {
        var current = new Version(0, 2, 7);
        var response = HttpResponseData.Canned(200, Encoding.UTF8.GetBytes("""
            {
              "tag_name": "v0.2.8",
              "assets": [
                { "name": "Piper-0.2.8-setup.exe", "browser_download_url": "https://github.com/tomwolfgang/piper/releases/download/v0.2.8/Piper-0.2.8-setup.exe" },
                { "name": "SHA256SUMS.txt", "browser_download_url": "https://github.com/tomwolfgang/piper/releases/download/v0.2.8/SHA256SUMS.txt" }
              ]
            }
            """), "application/json");

        var available = UpdateService.ParseLatestReleaseResponse(current, response);
        runner.IsTrue(available.IsUpdateAvailable, "a newer release with verified asset names is offered");
        runner.AreEqual("Piper-0.2.8-setup.exe", available.Release!.InstallerName, "installer asset selected");

        var currentRelease = UpdateService.ParseLatestReleaseResponse(new Version(0, 2, 8), response);
        runner.IsTrue(!currentRelease.IsUpdateAvailable && currentRelease.Error is null, "current release is not offered again");

        var wrongHost = HttpResponseData.Canned(200, Encoding.UTF8.GetBytes("""
            { "tag_name": "v0.2.9", "assets": [
              { "name": "Piper-0.2.9-setup.exe", "browser_download_url": "https://example.invalid/Piper-0.2.9-setup.exe" },
              { "name": "SHA256SUMS.txt", "browser_download_url": "https://github.com/tomwolfgang/piper/releases/download/v0.2.9/SHA256SUMS.txt" }
            ] }
            """), "application/json");
        var rejected = UpdateService.ParseLatestReleaseResponse(current, wrongHost);
        runner.IsTrue(rejected.Error is not null, "installer URLs outside GitHub are rejected");

        var malformed = HttpResponseData.Canned(200, Encoding.UTF8.GetBytes("{not-json"), "application/json");
        runner.IsTrue(UpdateService.ParseLatestReleaseResponse(current, malformed).Error is not null,
            "malformed release metadata is reported");

        return Task.CompletedTask;
    });
}
