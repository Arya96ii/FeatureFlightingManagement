﻿using System;
using System.Linq;
using System.Text;
using CQRS.Mediatr.Lite.SDK.Domain;
using Microsoft.FeatureFlighting.Common;
using Microsoft.FeatureFlighting.Common.Model;
using Microsoft.FeatureFlighting.Core.Optimizer;
using Microsoft.FeatureFlighting.Core.Domain.Events;
using Microsoft.FeatureFlighting.Common.AppExceptions;
using Microsoft.FeatureFlighting.Core.Domain.Assembler;
using Microsoft.FeatureFlighting.Core.Domain.ValueObjects;
using Microsoft.FeatureFlighting.Common.Model.AzureAppConfig;

namespace Microsoft.FeatureFlighting.Core.Domain
{
    internal class FeatureFlightAggregateRoot : AggregateRoot
    {
        public Feature Feature { get; private set; }
        public Status Status { get; private set; }
        public Tenant Tenant { get; private set; }
        public Settings Settings { get; private set; }
        public Condition Condition { get; private set; }
        public ValueObjects.Version Version { get; private set; }
        public Audit Audit { get; private set; }
        public Metric EvaluationMetrics { get; private set; }
        public AzureFeatureFlag ProjectedFlag { get; private set; }

        public FeatureFlightAggregateRoot(Feature feature,
            Status status,
            Tenant tenant,
            Settings settings,
            Condition condition,
            ValueObjects.Version version) : base(null)
        {
            Feature = feature;
            Status = status;
            Tenant = tenant;
            Settings = settings;
            Condition = condition;
            Version = version;
            _id = GetFlightId();
        }

        public FeatureFlightAggregateRoot(Feature feature,
            Status status,
            Tenant tenant,
            Settings settings,
            Condition condition,
            ValueObjects.Version version,
            Audit audit,
            Metric evaluationMetrics) : this(feature,
                status,
                tenant,
                settings,
                condition,
                version)
        {
            Audit = audit;
            EvaluationMetrics = evaluationMetrics;
        }

        public void CreateFeatureFlag(IFlightOptimizer optimizer, string createdBy, LoggerTrackingIds trackingIds)
        {
            Audit = new Audit(createdBy, System.DateTime.UtcNow, Status.Enabled);
            Version = new ValueObjects.Version();
            Status.UpdateActiveStatus(Condition);
            Condition.Validate(trackingIds);
            ProjectAzureFlag(optimizer, trackingIds);
            ApplyChange(new FeatureFlightCreated(this, trackingIds));
        }

        public void UpdateFeatureFlag(IFlightOptimizer optimizer, AzureFeatureFlag updatedFlag, string updatedBy, LoggerTrackingIds trackingIds, out bool isUpdated)
        {
            FeatureFlightDto originalFlag = FeatureFlightDtoAssembler.Assemble(this);
            Status newStatus = new(updatedFlag.Enabled, updatedFlag.IsFlagOptimized);
            bool isStatusUpdated = false;
            if (Status.Enabled != newStatus.Enabled)
            {
                Status = newStatus;
                isStatusUpdated = true;
                if (Status.Enabled)
                    ApplyChange(new FeatureFlightEnabled(this, trackingIds));
                else
                    ApplyChange(new FeatureFlightDisabled(this, trackingIds));
                Audit.UpdateEnabledStatus(updatedBy, DateTime.UtcNow, Status.Enabled);
            }

            Condition newCondition = new(updatedFlag.IncrementalRingsEnabled, updatedFlag.Conditions);
            var (areStageSettingsUpdated, areStagesUpdated, areStagesAdded, areStagesDeleted) = Condition.Update(newCondition);
            Status.UpdateActiveStatus(Condition);

            isUpdated = isStatusUpdated || areStageSettingsUpdated || areStagesUpdated || areStagesAdded || areStagesDeleted;
            if (!isUpdated)
                return;
            Condition.Validate(trackingIds);
            if (areStagesAdded || areStagesDeleted)
                Version.UpdateMajor();
            else
                Version.UpdateMinor();

            ProjectAzureFlag(optimizer, trackingIds);
            string updateType = GetUpdateType(isStatusUpdated, areStageSettingsUpdated, areStagesUpdated, areStagesAdded, areStagesDeleted);
            Audit.Update(updatedBy, DateTime.UtcNow, updateType);
            ApplyChange(new FeatureFlightUpdated(this, originalFlag, updateType, trackingIds));
        }

        public void Enable(string enabledBy, IFlightOptimizer optimizer, LoggerTrackingIds trackingIds, out bool isUpdated)
        {
            if (Status.Enabled)
            {
                isUpdated = false;
                return;
            }

            Status.Toggle();
            Status.UpdateActiveStatus(Condition);
            Version.UpdateMinor();
            Audit.UpdateEnabledStatus(enabledBy, DateTime.UtcNow, Status.Enabled);
            ProjectAzureFlag(optimizer, trackingIds);
            isUpdated = true;
            ApplyChange(new FeatureFlightEnabled(this, trackingIds));
        }

        public void Disable(string disabledBy, IFlightOptimizer optimizer, LoggerTrackingIds trackingIds, out bool isUpdated)
        {
            if (!Status.Enabled)
            {
                isUpdated = false;
                return;
            }

            Status.Toggle();
            Status.UpdateActiveStatus(Condition);
            Version.UpdateMinor();
            Audit.UpdateEnabledStatus(disabledBy, DateTime.UtcNow, Status.Enabled);
            ProjectAzureFlag(optimizer, trackingIds);
            isUpdated = true;
            ApplyChange(new FeatureFlightDisabled(this, trackingIds));
        }

        public void ActivateStage(string stageName, string activatedBy, IFlightOptimizer optimizer, LoggerTrackingIds trackingIds, out bool isStageActivated)
        {
            isStageActivated = false;
            Stage stage = Condition.Stages.FirstOrDefault(s => s.Name.ToLowerInvariant() == stageName.ToLowerInvariant());
            if (stage == null)
                throw new DomainException($"Stage with name {stageName} doesn't exist in flight for feature {Feature.Name}", "ACTIVATE_STAGE_001",
                    trackingIds.CorrelationId, trackingIds.TransactionId, "FeatureFlightAggregateRoot:ActivateStage");

            if (stage.IsActive && Condition.GetHighestActiveStage().Id == stage.Id)
                return;

            stage.Activate();

            foreach (Stage lowerStage in Condition.Stages.Where(s => s.Id < stage.Id))
            {
                if (Condition.IncrementalActivation)
                    lowerStage.Activate();
                else
                    lowerStage.Deactivate();
            }

            foreach (Stage higerStage in Condition.Stages.Where(s => s.Id > stage.Id))
            {
                higerStage.Deactivate();
            }
            isStageActivated = true;
            Version.UpdateMinor();
            Audit.Update(activatedBy, DateTime.UtcNow, "Stage Activated");
            ProjectAzureFlag(optimizer, trackingIds);
            ApplyChange(new FeatureFlightStageActivated(this, stageName, trackingIds));
        }

        public void Delete(string deletedBy, LoggerTrackingIds trackingIds)
        {
            Audit.Update(deletedBy, DateTime.UtcNow, "Flight Deleted");
            ApplyChange(new FeatureFlightDeleted(this, trackingIds));
        }

        public void ReBuild(string triggeredBy, string reason, IFlightOptimizer optimizer, LoggerTrackingIds trackingIds)
        {
            Audit.Update(triggeredBy, DateTime.UtcNow, $"Flight Rebuild - {reason}");
            Version.UpdateMinor();
            ProjectAzureFlag(optimizer, trackingIds);
            ApplyChange(new FeatureFlightRebuilt(this, reason, trackingIds));
        }

        private string GetFlightId()
        {
            return new StringBuilder()
                .Append(Tenant.Id.ToLowerInvariant())
                .Append("_")
                .Append(Tenant.Environment.ToLowerInvariant())
                .Append("_")
                .Append(Feature.Name)
                .ToString();
        }

        private void ProjectAzureFlag(IFlightOptimizer optimizer, LoggerTrackingIds trackingIds)
        {
            ProjectedFlag = AzureFeatureFlagAssember.Assemble(this);
            if (Settings.EnableOptimization)
            {
                optimizer.Optmize(ProjectedFlag, Settings.OptimizationRules, trackingIds);
                Status.SetOptimizationStatus(ProjectedFlag.IsFlagOptimized);
            }
        }

        private string GetUpdateType(bool isStatusUpdated, bool areStageSettingsUpdated, bool areStagesUpdated, bool areStagesAdded, bool areStagesDeleted)
        {
            StringBuilder updateType = new();
            if (isStatusUpdated)
            {
                if (Status.Enabled)
                    updateType.Append("Flag Enabled");
                else
                    updateType.Append("Flag Disabled");
                updateType.Append(" | ");
            }

            if (areStageSettingsUpdated)
            {
                updateType.Append("Stage Settings Updated").Append(" | ");
            }

            if (areStagesUpdated)
            {
                updateType.Append("Filters changed").Append(" | ");
            }

            if (areStagesAdded)
            {
                updateType.Append("New Stage Added").Append(" | ");
            }

            if (areStagesDeleted)
            {
                updateType.Append("Existing Stage Deleted").Append(" | ");
            }

            return updateType.ToString()[..(updateType.Length - 3)];
        }
    }
}
