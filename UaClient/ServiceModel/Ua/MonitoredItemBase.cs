﻿// Copyright (c) Converter Systems LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using System.Threading;
using Workstation.Collections;

namespace Workstation.ServiceModel.Ua
{
    /// <summary>
    /// Subscribes to data changes or events of an attribute of a node.
    /// </summary>
    public abstract class MonitoredItemBase
    {
        private static long lastClientId;

        /// <summary>
        /// Initializes a new instance of the <see cref="MonitoredItemBase"/> class.
        /// </summary>
        /// <param name="property">the property of the target to store the published value.</param>
        /// <param name="nodeId">the ExpandedNodeId to monitor.</param>
        /// <param name="attributeId">the attribute to monitor.</param>
        /// <param name="indexRange">the range of array indexes to monitor.</param>
        /// <param name="monitoringMode">the monitoring mode.</param>
        /// <param name="samplingInterval">the sampling interval.</param>
        /// <param name="filter">the properties that trigger a notification.</param>
        /// <param name="queueSize">the length of the queue used by the server to buffer values.</param>
        /// <param name="discardOldest">a value indicating whether to discard the oldest entries in the queue when it is full.</param>
        public MonitoredItemBase(PropertyInfo property, ExpandedNodeId nodeId, uint attributeId = AttributeIds.Value, string indexRange = null, MonitoringMode monitoringMode = MonitoringMode.Reporting, int samplingInterval = -1, MonitoringFilter filter = null, uint queueSize = 0, bool discardOldest = true)
        {
            if (property == null)
            {
                throw new ArgumentNullException(nameof(property));
            }

            if (nodeId == null)
            {
                throw new ArgumentNullException(nameof(nodeId));
            }

            this.Property = property;
            this.NodeId = nodeId;
            this.AttributeId = attributeId;
            this.IndexRange = indexRange;
            this.MonitoringMode = monitoringMode;
            this.SamplingInterval = samplingInterval;
            this.Filter = filter;
            this.QueueSize = queueSize;
            this.DiscardOldest = discardOldest;
            this.ClientId = (uint)Interlocked.Increment(ref lastClientId);
        }

        /// <summary>
        /// Gets the name of the property of the target.
        /// </summary>
        public string Name => this.Property.Name;

        /// <summary>
        /// Gets the property of the target to store the published value.
        /// </summary>
        public PropertyInfo Property { get; }

        /// <summary>
        /// Gets the ExpandedNodeId to monitor.
        /// </summary>
        public ExpandedNodeId NodeId { get; }

        /// <summary>
        /// Gets the attribute to monitor.
        /// </summary>
        public uint AttributeId { get; }

        /// <summary>
        /// Gets the range of array indexes to monitor.
        /// </summary>
        public string IndexRange { get; }

        /// <summary>
        /// Gets the monitoring mode.
        /// </summary>
        public MonitoringMode MonitoringMode { get; }

        /// <summary>
        /// Gets the sampling interval.
        /// </summary>
        public int SamplingInterval { get; }

        /// <summary>
        /// Gets the filter used by the server to select values to return.
        /// </summary>
        public MonitoringFilter Filter { get; }

        /// <summary>
        /// Gets the length of the queue used by the server to buffer values.
        /// </summary>
        public uint QueueSize { get; }

        /// <summary>
        /// Gets a value indicating whether to discard the oldest entries in the queue when it is full.
        /// </summary>
        public bool DiscardOldest { get; }

        /// <summary>
        /// Gets the identifier assigned by the client.
        /// </summary>
        public uint ClientId { get; }

        /// <summary>
        /// Gets or sets the identifier assigned by the server.
        /// </summary>
        public uint ServerId { get; protected set; }

        public abstract void Publish(object target, DataValue dataValue);

        public abstract void Publish(object target, Variant[] eventFields);

        public abstract bool TryGetValue(object target, out DataValue value);

        public abstract void OnCreateResult(object target, MonitoredItemCreateResult result);

        public abstract void OnWriteResult(object target, StatusCode statusCode);
    }

    /// <summary>
    /// Subscribes to data changes of an attribute of a node.
    /// Sets the published value in a property of type DataValue.
    /// </summary>
    public class DataValueMonitoredItem : MonitoredItemBase
    {
        private StatusCode lastStatusCode;

        public DataValueMonitoredItem(PropertyInfo property, ExpandedNodeId nodeId, uint attributeId = 13, string indexRange = null, MonitoringMode monitoringMode = MonitoringMode.Reporting, int samplingInterval = -1, MonitoringFilter filter = null, uint queueSize = 0, bool discardOldest = true)
            : base(property, nodeId, attributeId, indexRange, monitoringMode, samplingInterval, filter, queueSize, discardOldest)
        {
        }

        public override void Publish(object target, DataValue dataValue)
        {
            this.Property.SetValue(target, dataValue);
            this.SetDataErrorInfo(target, dataValue.StatusCode);
        }

        public override void Publish(object target, Variant[] eventFields)
        {
        }

        public override bool TryGetValue(object target, out DataValue value)
        {
            var pi = this.Property;
            if (pi.CanRead)
            {
                value = (DataValue)pi.GetValue(target);
                return true;
            }
            value = default(DataValue);
            return false;
        }

        public override void OnCreateResult(object target, MonitoredItemCreateResult result)
        {
            this.ServerId = result.MonitoredItemId;
            this.SetDataErrorInfo(target, result.StatusCode);
        }

        public override void OnWriteResult(object target, StatusCode statusCode)
        {
            this.SetDataErrorInfo(target, statusCode);
        }

        private void SetDataErrorInfo(object target, StatusCode statusCode)
        {
            if (this.lastStatusCode == statusCode)
            {
                return;
            }

            this.lastStatusCode = statusCode;
            var targetAsDataErrorInfo = target as ISetDataErrorInfo;
            if (targetAsDataErrorInfo != null)
            {
                if (!StatusCode.IsGood(statusCode))
                {
                    targetAsDataErrorInfo.SetErrors(this.Property.Name, new string[] { StatusCodes.GetDefaultMessage(statusCode) });
                }
                else
                {
                    targetAsDataErrorInfo.SetErrors(this.Property.Name, null);
                }
            }
        }
    }

    /// <summary>
    /// Subscribes to data changes of an attribute of a node.
    /// Unwraps the published value and sets it in a property.
    /// </summary>
    public class ValueMonitoredItem : MonitoredItemBase
    {
        private StatusCode lastStatusCode;

        public ValueMonitoredItem(PropertyInfo property, ExpandedNodeId nodeId, uint attributeId = 13, string indexRange = null, MonitoringMode monitoringMode = MonitoringMode.Reporting, int samplingInterval = -1, MonitoringFilter filter = null, uint queueSize = 0, bool discardOldest = true)
            : base(property, nodeId, attributeId, indexRange, monitoringMode, samplingInterval, filter, queueSize, discardOldest)
        {
        }

        public override void Publish(object target, DataValue dataValue)
        {
            this.Property.SetValue(target, dataValue.GetValue());
            this.SetDataErrorInfo(target, dataValue.StatusCode);
        }

        public override void Publish(object target, Variant[] eventFields)
        {
        }

        public override bool TryGetValue(object target, out DataValue value)
        {
            var pi = this.Property;
            if (pi.CanRead)
            {
                value = new DataValue(this.Property.GetValue(target));
                return true;
            }
            value = default(DataValue);
            return false;
        }

        public override void OnCreateResult(object target, MonitoredItemCreateResult result)
        {
            this.ServerId = result.MonitoredItemId;
            this.SetDataErrorInfo(target, result.StatusCode);
        }

        public override void OnWriteResult(object target, StatusCode statusCode)
        {
            this.SetDataErrorInfo(target, statusCode);
        }

        private void SetDataErrorInfo(object target, StatusCode statusCode)
        {
            if (this.lastStatusCode == statusCode)
            {
                return;
            }

            this.lastStatusCode = statusCode;
            var targetAsDataErrorInfo = target as ISetDataErrorInfo;
            if (targetAsDataErrorInfo != null)
            {
                if (!StatusCode.IsGood(statusCode))
                {
                    targetAsDataErrorInfo.SetErrors(this.Property.Name, new string[] { StatusCodes.GetDefaultMessage(statusCode) });
                }
                else
                {
                    targetAsDataErrorInfo.SetErrors(this.Property.Name, null);
                }
            }
        }
    }

    /// <summary>
    /// Subscribes to data changes of an attribute of a node.
    /// Enqueues the published value to an <see cref="ObservableQueue{DataValue}"/>.
    /// </summary>
    public class DataValueQueueMonitoredItem : MonitoredItemBase
    {
        private StatusCode lastStatusCode;

        public DataValueQueueMonitoredItem(PropertyInfo property, ExpandedNodeId nodeId, uint attributeId = 13, string indexRange = null, MonitoringMode monitoringMode = MonitoringMode.Reporting, int samplingInterval = -1, MonitoringFilter filter = null, uint queueSize = 0, bool discardOldest = true)
            : base(property, nodeId, attributeId, indexRange, monitoringMode, samplingInterval, filter, queueSize, discardOldest)
        {
        }

        public override void Publish(object target, DataValue dataValue)
        {
            var queue = (ObservableQueue<DataValue>)this.Property.GetValue(target);
            queue.Enqueue(dataValue);
        }

        public override void Publish(object target, Variant[] eventFields)
        {
        }

        public override bool TryGetValue(object target, out DataValue value)
        {
            value = default(DataValue);
            return false;
        }

        public override void OnCreateResult(object target, MonitoredItemCreateResult result)
        {
            this.ServerId = result.MonitoredItemId;
            this.SetDataErrorInfo(target, result.StatusCode);
        }

        public override void OnWriteResult(object target, StatusCode statusCode)
        {
            this.SetDataErrorInfo(target, statusCode);
        }

        private void SetDataErrorInfo(object target, StatusCode statusCode)
        {
            if (this.lastStatusCode == statusCode)
            {
                return;
            }

            this.lastStatusCode = statusCode;
            var targetAsDataErrorInfo = target as ISetDataErrorInfo;
            if (targetAsDataErrorInfo != null)
            {
                if (!StatusCode.IsGood(statusCode))
                {
                    targetAsDataErrorInfo.SetErrors(this.Property.Name, new string[] { StatusCodes.GetDefaultMessage(statusCode) });
                }
                else
                {
                    targetAsDataErrorInfo.SetErrors(this.Property.Name, null);
                }
            }
        }
    }

    /// <summary>
    /// Subscribes to events of an attribute of a node.
    /// Sets the published event in a property of type BaseEvent or subtype.
    /// </summary>
    public class EventMonitoredItem : MonitoredItemBase
    {
        private StatusCode lastStatusCode;

        public EventMonitoredItem(PropertyInfo property, ExpandedNodeId nodeId, uint attributeId = 12, string indexRange = null, MonitoringMode monitoringMode = MonitoringMode.Reporting, int samplingInterval = -1, MonitoringFilter filter = null, uint queueSize = 0, bool discardOldest = true)
            : base(property, nodeId, attributeId, indexRange, monitoringMode, samplingInterval, filter, queueSize, discardOldest)
        {
        }

        public override void Publish(object target, DataValue dataValue)
        {
        }

        public override void Publish(object target, Variant[] eventFields)
        {
            var currentEvent = EventHelper.Deserialize(this.Property.PropertyType, eventFields);
            this.Property.SetValue(target, currentEvent);
        }

        public override bool TryGetValue(object target, out DataValue value)
        {
            value = default(DataValue);
            return false;
        }

        public override void OnCreateResult(object target, MonitoredItemCreateResult result)
        {
            this.ServerId = result.MonitoredItemId;
            this.SetDataErrorInfo(target, result.StatusCode);
        }

        public override void OnWriteResult(object target, StatusCode statusCode)
        {
            this.SetDataErrorInfo(target, statusCode);
        }

        private void SetDataErrorInfo(object target, StatusCode statusCode)
        {
            if (this.lastStatusCode == statusCode)
            {
                return;
            }

            this.lastStatusCode = statusCode;
            var targetAsDataErrorInfo = target as ISetDataErrorInfo;
            if (targetAsDataErrorInfo != null)
            {
                if (!StatusCode.IsGood(statusCode))
                {
                    targetAsDataErrorInfo.SetErrors(this.Property.Name, new string[] { StatusCodes.GetDefaultMessage(statusCode) });
                }
                else
                {
                    targetAsDataErrorInfo.SetErrors(this.Property.Name, null);
                }
            }
        }
    }

    /// <summary>
    /// Subscribes to events of an attribute of a node.
    /// Enqueues the published event to an <see cref="ObservableQueue{T}"/>.
    /// </summary>
    public class EventQueueMonitoredItem<T> : MonitoredItemBase
            where T : BaseEvent, new()
    {
        private StatusCode lastStatusCode;

        public EventQueueMonitoredItem(PropertyInfo property, ExpandedNodeId nodeId, uint attributeId = 12, string indexRange = null, MonitoringMode monitoringMode = MonitoringMode.Reporting, int samplingInterval = -1, MonitoringFilter filter = null, uint queueSize = 0, bool discardOldest = true)
            : base(property, nodeId, attributeId, indexRange, monitoringMode, samplingInterval, filter, queueSize, discardOldest)
        {
        }

        public override void Publish(object target, DataValue dataValue)
        {
        }

        public override void Publish(object target, Variant[] eventFields)
        {
            var currentEvent = EventHelper.Deserialize<T>(eventFields);
            var queue = (ObservableQueue<T>)this.Property.GetValue(target);
            queue.Enqueue(currentEvent);
        }

        public override bool TryGetValue(object target, out DataValue value)
        {
            value = default(DataValue);
            return false;
        }

        public override void OnCreateResult(object target, MonitoredItemCreateResult result)
        {
            this.ServerId = result.MonitoredItemId;
            this.SetDataErrorInfo(target, result.StatusCode);
        }

        public override void OnWriteResult(object target, StatusCode statusCode)
        {
            this.SetDataErrorInfo(target, statusCode);
        }

        private void SetDataErrorInfo(object target, StatusCode statusCode)
        {
            if (this.lastStatusCode == statusCode)
            {
                return;
            }

            this.lastStatusCode = statusCode;
            var targetAsDataErrorInfo = target as ISetDataErrorInfo;
            if (targetAsDataErrorInfo != null)
            {
                if (!StatusCode.IsGood(statusCode))
                {
                    targetAsDataErrorInfo.SetErrors(this.Property.Name, new string[] { StatusCodes.GetDefaultMessage(statusCode) });
                }
                else
                {
                    targetAsDataErrorInfo.SetErrors(this.Property.Name, null);
                }
            }
        }
    }
}