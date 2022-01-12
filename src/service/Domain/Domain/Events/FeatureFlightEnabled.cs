﻿using System.Collections.Generic;
using Microsoft.FeatureFlighting.Common;

namespace Microsoft.FeatureFlighting.Core.Domain.Events
{
    /// <summary>
    /// Event when a feature flight is enabled
    /// </summary>
    public class FeatureFlightEnabled : BaseFeatureFlightEvent
    {
        public override string DisplayName => nameof(FeatureFlightEnabled);

        public string EnabledBy { get; set; }

        public FeatureFlightEnabled(FeatureFlightAggregateRoot flight,  LoggerTrackingIds trackingIds)
            :base(flight, trackingIds)
        {
            EnabledBy = flight.Audit.LastModifiedBy;
        }

        public override Dictionary<string, string> GetProperties()
        {
            Dictionary<string, string> properties = base.GetProperties();
            properties.AddOrUpdate(nameof(EnabledBy), EnabledBy);
            return properties;
        }
    }
}
