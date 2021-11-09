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
using Realtorist.Extensions.Base.Manager;
using Realtorist.Services.Abstractions.Events;
using Realtorist.Models.Events;

namespace Realtorist.RetsClient.Implementations.Composite
{
    public class CompositeUpdateFlow : IUpdateFlow
    {
        private readonly ISettingsProvider _settingsProvider;
        private readonly IServiceProvider _serviceProvider;
        private readonly IExtensionManager _extensionManager;
        private readonly IEventLogger _eventLogger;
        private readonly ILogger _logger;

        public CompositeUpdateFlow(
            ISettingsProvider settingsProvider, 
            IServiceProvider serviceProvider, 
            IExtensionManager extensionManager,
            IEventLogger eventLogger,
            ILogger<CompositeUpdateFlow> logger)
        {
            _settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _extensionManager = extensionManager ?? throw new ArgumentNullException(nameof(extensionManager));
            _eventLogger = eventLogger ?? throw new ArgumentNullException(nameof(eventLogger));
        }

        public async Task<int> LaunchAsync()
        {
            var settings = await _settingsProvider.GetSettingAsync<ListingsSettings>(SettingTypes.Listings);
            if (settings is null || settings.Feeds.IsNullOrEmpty())
            {
                _logger.LogWarning($"Skipping listings feeds update. No settings were found");
                return 0;
            }

            var websiteSettings = await _settingsProvider.GetSettingAsync<WebsiteSettings>(SettingTypes.Website);

            var utcNow = DateTime.UtcNow;
            var now = websiteSettings.GetDateTimeInTimeZoneFromUtc(utcNow);

            var tasks = new List<Task<int>>();
            foreach(var source in settings.Feeds)
            {

                if (source.UpdateTime.IsNullOrEmpty()) continue;
                var cron = CronExpression.Parse(source.UpdateTime);
                var next = cron.GetNextOccurrence(utcNow.AddMinutes(-1), websiteSettings.TimezoneInfo);
                if (!next.HasValue || !next.Value.EqualsToMinute(utcNow))
                {
                    continue;
                }

                _logger.LogInformation($"Time: {now}. Launching update flow for source '{source.FeedType}'");

                var implementation = _extensionManager.GetInstances<IListingsFeedExtension>().FirstOrDefault(ext => ext.Name == source.FeedType);
                if (implementation is null)
                {
                    var message = $"Can't find extension for listing feed of type '{source.FeedType}'. Skipping update";
                    
                    _logger.LogWarning(message);
                    await _eventLogger.CreateEventAsync(EventLevel.Warning, EventTypes.ListingUpdate, "Unknown feed", message);
                    
                    continue;
                }

                var flow = (IUpdateFlow)ActivatorUtilities.CreateInstance(_serviceProvider, implementation.UpdateFlowType, Options.Create(source));

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
