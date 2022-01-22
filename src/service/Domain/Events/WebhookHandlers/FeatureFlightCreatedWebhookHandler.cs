﻿using AppInsights.EnterpriseTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.FeatureFlighting.Common.Config;
using Microsoft.FeatureFlighting.Common.Webhook;
using Microsoft.FeatureFlighting.Core.Domain.Events;

namespace Microsoft.FeatureFlighting.Core.Events.WebhookHandlers
{
    internal class FeatureFlightCreatedWebhookHandler : BaseFeatureFlightWebhookEventHandler<FeatureFlightCreated>
    {
        protected override string NotificationSubject => _emailConfiguration.FeatureFlightCreatedEmailSubject;

        protected override string NotificationContent => _emailConfiguration.FeatureFlightCreatedEmailTemplate;

        public FeatureFlightCreatedWebhookHandler(ITenantConfigurationProvider tenantConfigurationProvider, IWebhookTriggerManager webhookTriggerManager, IConfiguration configuration, ILogger logger)
            :base(tenantConfigurationProvider, webhookTriggerManager, configuration, logger)
        { }
    }
}
