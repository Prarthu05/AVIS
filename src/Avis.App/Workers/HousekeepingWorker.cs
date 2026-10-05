using Avis.Imaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Avis.App.Workers;

/// <summary>Daily clean-up of staged IV4 images older than ImageStaging:RetentionDays, so the station PC's disk doesn't fill.</summary>
public class HousekeepingWorker : BackgroundService
{
    private readonly ImageStager _images;
    private readonly ImageStagingOptions _options;
    private readonly ILogger<HousekeepingWorker> _logger;
    private readonly TimeProvider _time;

    public HousekeepingWorker(ImageStager images, ImageStagingOptions options, ILogger<HousekeepingWorker> logger, TimeProvider time)
    {
        _images = images;
        _options = options;
        _logger = logger;
        _time = time;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        try
        {
            // The IV4 pushes into this folder - make sure it exists so the first
            // image isn't lost to a "folder not found".
            Directory.CreateDirectory(Avis.Configuration.AvisOptions.ResolvePath(_options.DropFolder));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not create the IV4 image drop folder {Folder}", _options.DropFolder);
        }

        if (_options.RetentionDays <= 0)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _images.CleanupOldImages(_time.GetLocalNow());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Image clean-up failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
