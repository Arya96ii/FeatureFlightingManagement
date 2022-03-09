﻿using System;
using CQRS.Mediatr.Lite;
using System.Collections.Generic;
using Microsoft.FeatureFlighting.Common.Config;

namespace Microsoft.FeatureFlighting.Core.Queries
{
    /// <summary>
    /// Queries tenants registered in the app service
    /// </summary>
    public class GetRegisteredTenantsQuery : Query<IEnumerable<TenantConfiguration>>
    {
        public override string DisplayName => nameof(GetRegisteredTenantsQuery);

        public override string Id { get; }
        public bool IncludeDynamicTenants { get; set; }

        public GetRegisteredTenantsQuery(bool includeDynamicTenants)
        {
            Id = Guid.NewGuid().ToString();
            IncludeDynamicTenants = includeDynamicTenants;
        }

        public override bool Validate(out string ValidationErrorMessage)
        {
            ValidationErrorMessage = null;
            return true;
        }
    }
}
