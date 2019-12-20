using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Internal;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dotnetes.Operator
{
    /// <summary>
    /// <see cref="BackgroundService"/> that triggers the <see cref="DotnetesOperator"/> to run at a regular interval.
    /// </summary>
    internal class DotnetesOperatorService : BackgroundService
    {
        private readonly DotnetesOperator _operator;
        private readonly ILogger<DotnetesOperatorService> _logger;
        private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly IOptionsMonitor<DotnetesOperatorOptions> _options;

        public DotnetesOperatorService(
            DotnetesOperator @operator, 
            ILogger<DotnetesOperatorService> logger,
            IHostApplicationLifetime applicationLifetime, 
            IOptionsMonitor<DotnetesOperatorOptions> options)
        {
            _operator = @operator;
            _logger = logger;
            _applicationLifetime = applicationLifetime;
            _options = options;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var currentOptions = _options.CurrentValue;
            var timer = new TimerAwaitable(TimeSpan.Zero, currentOptions.CheckInterval);

            // Can't use a real discard here :(
            using var _1 = _options.OnChange(options => {
                _logger.LogDebug(new EventId(0, "UpdatingTimer"), "Updating timer interval to {Interval}", options.CheckInterval);
                timer.Change(TimeSpan.Zero, options.CheckInterval);
            });

            _logger.LogDebug(new EventId(0, "StartingTimer"), "Starting timer at interval {Interval}", currentOptions.CheckInterval);
            timer.Start();

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    await _operator.RunAsync();

                    _logger.LogDebug(new EventId(0, "Sleeping"), "Sleeping until next activation...");
                    await timer;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(0, "Error"), ex, "An unexpected error occurred during operation.");
            }
            finally
            {
                _logger.LogInformation(new EventId(0, "ShuttingDown"), "DotnetesOperatorService has terminated, the application must exit.");
                _applicationLifetime.StopApplication();
            }
        }
    }
}
