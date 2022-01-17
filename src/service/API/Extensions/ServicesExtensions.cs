﻿using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using Microsoft.FeatureFlighting.Api.ExceptionHandler;
using AppInsights.EnterpriseTelemetry.Web.Extension.Middlewares;
using AppInsights.EnterpriseTelemetry.Web.Extension;
using Microsoft.FeatureManagement;
using Microsoft.FeatureFlighting.Core.Operators;
using Microsoft.FeatureFlighting.Core.Spec;
using Microsoft.FeatureFlighting.Core.RulesEngine;
using Microsoft.FeatureFlighting.Common.Config;
using Microsoft.FeatureFlighting.Core.FeatureFilters;
using Microsoft.FeatureManagement.FeatureFilters;
using Microsoft.AspNetCore.Http;

namespace Microsoft.FeatureFlighting.API.Extensions
{
    internal static class ServicesExtensions
    {
        /// <summary>
        /// Adds authentication to the pipeline
        /// </summary>
        public static void AddAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                   .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
                   {

                       options.Authority = configuration["Authentication:Authority"];
                       var primaryAudience = configuration["Authentication:Audience"];
                       IList<string> validAudiences = !string.IsNullOrWhiteSpace(configuration["Authentication:AdditionalAudiences"])
                        ? configuration["Authentication:AdditionalAudiences"].Split(',').ToList()
                        : new List<string>();
                       validAudiences.Add(primaryAudience);
                       options.TokenValidationParameters = new IdentityModel.Tokens.TokenValidationParameters
                       {
                           ValidAudiences = validAudiences
                       };
                   })
                    // Adding support for MSAL
                    .AddJwtBearer("MSAL", options =>
                    {
                        options.Authority = configuration["Authentication:AuthorityV2"];
                        var primaryAudience = configuration["Authentication:Audience"];
                        IList<string> validAudiences = !string.IsNullOrWhiteSpace(configuration["Authentication:AdditionalAudiences"])
                         ? configuration["Authentication:AdditionalAudiences"].Split(',').ToList()
                         : new List<string>();
                        validAudiences.Add(primaryAudience);
                        options.TokenValidationParameters = new IdentityModel.Tokens.TokenValidationParameters
                        {
                            ValidAudiences = validAudiences
                        };
                    });
        }

        /// <summary>
        /// Adds Swagger documenation
        /// </summary>
        /// <remarks>
        /// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        /// </remarks>
        public static void AddSwagger(this IServiceCollection services)
        {
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Flighting Service",
                    Version = "v2",
                    Contact = new OpenApiContact
                    {
                        Email = "fxpswe@microsoft.com",
                        Name = "Field Experience Engineering Team",
                        Url = new System.Uri("https://aka.ms/fxpdocs")
                    },
                    Description = "APIs for managing and evaluating feature flags. Powered by Azure Configuration (Feature Management)"
                });
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    In = ParameterLocation.Header,
                    Description = "Please insert JWT with Bearer into field",
                    Name = "Authorization",
                    Type = SecuritySchemeType.ApiKey
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement {
                    {
                        new OpenApiSecurityScheme()
                        {
                            Reference = new OpenApiReference()
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        System.Array.Empty<string>()
                    }
                });
            });
            services.AddEndpointsApiExplorer();
        }

        /// <summary>
        /// Adds telemetry to API
        /// </summary>
        public static void AddTelememtry(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IGlobalExceptionHandler, DomainExceptionHandler>();
            services.AddSingleton<IGlobalExceptionHandler, AccessForbiddenExceptionHandler>();
            services.AddSingleton<IGlobalExceptionHandler, GenericExceptionHandler>();
            services.AddEnterpriseTelemetry(configuration);
        }

        public static void AddFeatureFilters(this IServiceCollection services)
        {
            services.AddAzureAppConfiguration();
            services.AddFeatureManagement()
                .AddFeatureFilter<AliasFilter>()
                .AddFeatureFilter<RoleGroupFilter>()
                .AddFeatureFilter<DateFilter>()
                .AddFeatureFilter<TimeWindowFilter>()
                .AddFeatureFilter<PercentageFilter>()
                .AddFeatureFilter<CountryFilter>()
                .AddFeatureFilter<RegionFilter>()
                .AddFeatureFilter<RoleFilter>()
                .AddFeatureFilter<UserUpnFilter>()
                .AddFeatureFilter<GenericFilter>()
                .AddFeatureFilter<RulesEngineFilter>();
        }

        public static void AddHttpClients(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        }
    }
}
