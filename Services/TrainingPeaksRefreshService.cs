using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using RecipeApp.Data;
using RecipeApp.Services;

namespace RecipeApp.Services
{
    /// <summary>
    /// Background job that refreshes TrainingPeaks calendar imports each Sunday at 00:01 UTC
    /// for users that have a TrainingPeaks ICS URL saved.
    /// </summary>
    public class TrainingPeaksRefreshService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TrainingPeaksRefreshService> _logger;

        public TrainingPeaksRefreshService(IServiceProvider serviceProvider, ILogger<TrainingPeaksRefreshService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = GetDelayUntilNextRun();
                _logger.LogInformation("TrainingPeaks refresh scheduled in {Delay}", delay);
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }

                if (stoppingToken.IsCancellationRequested) break;

                await RunRefreshAsync(stoppingToken);
            }
        }

        private async Task RunRefreshAsync(CancellationToken token)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var importService = scope.ServiceProvider.GetRequiredService<ICalendarImportService>();

            var users = await db.Users
                .Where(u => !string.IsNullOrWhiteSpace(u.TrainingPeaksIcsUrl))
                .ToListAsync(token);

            foreach (var user in users)
            {
                if (token.IsCancellationRequested) break;
                try
                {
                    _logger.LogInformation("Refreshing TrainingPeaks for user {User}", user.Email);
                    await importService.ImportAsync(user, user.TrainingPeaksIcsUrl!, "this");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to refresh TrainingPeaks for user {User}", user.Email);
                }
            }
        }

        private static TimeSpan GetDelayUntilNextRun()
        {
            var utcNow = DateTime.UtcNow;
            var target = NextSundayAt0001Utc(utcNow);
            return target - utcNow;
        }

        private static DateTime NextSundayAt0001Utc(DateTime from)
        {
            var daysUntilSunday = ((int)DayOfWeek.Sunday - (int)from.DayOfWeek + 7) % 7;
            var date = from.Date.AddDays(daysUntilSunday);
            var target = date.AddHours(0).AddMinutes(1);
            if (target <= from)
            {
                target = target.AddDays(7);
            }
            return target;
        }
    }
}
