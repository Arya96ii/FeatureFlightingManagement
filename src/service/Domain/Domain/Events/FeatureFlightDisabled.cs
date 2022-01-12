﻿using System.Collections.Generic;
using Microsoft.FeatureFlighting.Common;

namespace Microsoft.FeatureFlighting.Core.Domain.Events
{
    /// <summary>
    /// Event when a feature flight is disabled
    /// </summary>
    public class FeatureFlightDisabled : BaseFeatureFlightEvent
    {
        public override string DisplayName => nameof(FeatureFlightUpdated);

        public string DisabledBy { get; set; }

        public FeatureFlightDisabled(FeatureFlightAggregateRoot flight, LoggerTrackingIds trackingIds)
            : base(flight, trackingIds)
        {
            DisabledBy = flight.Audit.LastModifiedBy;
        }

        public override Dictionary<string, string> GetProperties()
        {
            Dictionary<string, string> properties = base.GetProperties();
            properties.AddOrUpdate(nameof(DisabledBy), DisabledBy);
            return properties;
        }
    }
}
