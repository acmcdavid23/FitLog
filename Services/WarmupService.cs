namespace FitLog.Services
{
    public class WarmupService : BackgroundService
    {
        private readonly IServiceProvider _services;

        public WarmupService(IServiceProvider services)
        {
            _services = services;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _services.CreateScope();
                    var context = scope.ServiceProvider
                        .GetRequiredService<FitLog.Data.ApplicationDbContext>();
                    _ = context.Exercises.Count();
                }
                catch { }

                await Task.Delay(TimeSpan.FromMinutes(9), stoppingToken);
            }
        }
    }
}