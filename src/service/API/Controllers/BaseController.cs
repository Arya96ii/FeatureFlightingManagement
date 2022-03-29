﻿using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.FeatureFlighting.Common.AppExceptions;
using AppInsights.EnterpriseTelemetry;
using AppInsights.EnterpriseTelemetry.Context;
using System.ComponentModel;

namespace Microsoft.FeatureFlighting.API.Controllers
{
    /// <summary>
    /// Base abstract controller for all controllers in Feature Management syste,
    /// </summary>
    [ApiController]
    [AspNetCore.Authorization.Authorize]
    public class BaseController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;

        /// <summary>
        /// Constructor
        /// </summary>
        public BaseController(IConfiguration configuration, ILogger logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Gets the common headers  - Tenant, Environment, CorrelationId, TransactionId, Channel
        /// </summary>
        /// <param name="validateHeaders">Flag to validate the headers</param>
        /// <returns>Tuple - (Tenant, Environment, CorrelationId, TransactionId, Channel)</returns>
        /// <exception cref="DomainException">If tenant or environment is not passed</exception>
        protected Tuple<string, string, string, string, string> GetHeaders(bool validateHeaders = true)
        {
            string tenant = GetHeaderValue("x-application");
            string environment = GetHeaderValue("x-environment");
            string correlationId = GetHeaderValue("x-correlationId", Guid.NewGuid().ToString());
            string transactionId = GetHeaderValue("x-messageId", Guid.NewGuid().ToString());
            string channel = GetHeaderValue("x-channel", "Developer Self-Serve");

            var headers = new Tuple<string, string, string, string, string>(tenant, environment, correlationId, transactionId, channel);

            if (!validateHeaders)
                return headers;

            if (string.IsNullOrWhiteSpace(tenant))
                throw new DomainException("Tenant name is missing. Tenant name needs to be passed in the x-application header", "CNTRL_001",
                    correlationId, transactionId, "FeatureFlagsAdminController:GetHeaders");

            if (string.IsNullOrWhiteSpace(environment))
                throw new DomainException("Enviornment name is missing. Enviornment needs to be passed in the x-environment header", "CNTRL_002",
                    correlationId, transactionId, "FeatureFlagsAdminController:GetHeaders");

            ValidateEnvironment(environment, correlationId, transactionId);

            return headers;
        }

        /// <summary>
        /// Gets the header value from the key
        /// </summary>
        /// <param name="headerKey">Header Key</param>
        /// <param name="defaultValue">Default value if the key is not present</param>
        /// <returns>Header value</returns>
        protected string GetHeaderValue(string headerKey, string defaultValue)
        {   
            try
            {
                string headerValue = GetHeaderValue(headerKey);
                if (string.IsNullOrWhiteSpace(headerValue))
                    return defaultValue;
                return headerValue;
            }
            catch (Exception exception)
            {
                // TODO - THE BELOW CODE HAS BEEN KEPT FOR DEBUGGING ERRATIC FAILURES IN PROD
                _logger.Log(exception);
                MessageContext messageContext = new($"Error in getting header for {headerKey ?? "INVALID"}", TraceLevel.Error, "", "", "", "", "");
                _logger.Log(messageContext);
                return defaultValue;
            }
        }

        /// <summary>
        /// Gets the header value from the key
        /// </summary>
        /// <param name="headerKey">Header Key</param>
        /// <returns>Header value</returns>
        protected string GetHeaderValue(string headerKey)
        {
            if (string.IsNullOrWhiteSpace(headerKey))
                return null;

            if (Request == null)
                return null;

            if (Request.Headers == null)
                return null;

            if (Request.Headers.TryGetValue(headerKey, out var value))
                return value.FirstOrDefault()?.ToString();

            return null;
        }

        private void ValidateEnvironment(string envName, string correlationId, string transactionId)
        {
            var supportedEnvironments = _configuration.GetSection("Env:Supported").Value.Split(",");
            bool isSupported = supportedEnvironments.Any(supportedEnvironment => supportedEnvironment.ToLowerInvariant() == envName.ToLowerInvariant());
            if (!isSupported)
                throw new DomainException("Provided environment is not supported", "CNTRL_003",
                    correlationId, transactionId, "FeatureFlagsAdminController:GetHeaders:ValidateEnvironment");
        }
    }
}
