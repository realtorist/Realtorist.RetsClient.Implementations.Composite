using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Cronos;
using Realtorist.Models.Helpers;
using Realtorist.Models.Settings;
using Realtorist.RetsClient.Abstractions;
using Realtorist.Services.Abstractions.Providers;

namespace Realtorist.RetsClient.Implementations.Composite
{
    public class CompositeUpdateFlow : IUpdateFlow
    {
        private readonly ISettingsProvider _settingsProvider;
        private readonly IServiceProvider _serviceProvider;
        private readonly IUpdateFlowFactory _updateFlowFactory;
        private ILogger _logger;

        public CompositeUpdateFlow(ISettingsProvider settingsProvider, IServiceProvider serviceProvider, IUpdateFlowFactory updateFlowFactory, ILogger<CompositeUpdateFlow> logger)
        {
            _settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _updateFlowFactory = updateFlowFactory ?? throw new ArgumentNullException(nameof(updateFlowFactory));
        }

        public async Task<int> LaunchAsync()
        {
            var sources = await _settingsProvider.GetSettingAsync<RetsConfiguration[]>(SettingTypes.ListingSources);
            var websiteSettings = await _settingsProvider.GetSettingAsync<WebsiteSettings>(SettingTypes.Website);

            var utcNow = DateTime.UtcNow;
            var now = websiteSettings.GetDateTimeInTimeZoneFromUtc(utcNow);

            var tasks = new List<Task<int>>();
            foreach(var source in sources)
            {

                if (source.UpdateTime.IsNullOrEmpty()) continue;
                var cron = CronExpression.Parse(source.UpdateTime);
                var next = cron.GetNextOccurrence(utcNow.AddMinutes(-1), websiteSettings.TimezoneInfo);
                if (!next.HasValue || !next.Value.EqualsToMinute(utcNow))
                {
                    continue;
                }

                _logger.LogInformation($"Time: {now}. Launching update flow for source '{source.ListingSource}'");

                var type = _updateFlowFactory.GetUpdateFlowType(source.ListingSource);
                var flow = (IUpdateFlow)ActivatorUtilities.CreateInstance(_serviceProvider, type, Options.Create(source));

                var task = flow.LaunchAsync();
                tasks.Add(task);
            }

            var results = await Task.WhenAll(tasks);
            var count = results.Sum();

            _logger.LogInformation($"Total results: {count}");

            return count;
        }
    }
}
