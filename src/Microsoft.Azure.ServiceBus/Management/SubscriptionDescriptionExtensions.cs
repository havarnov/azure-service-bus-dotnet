﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus.Management
{
    using System;
    using System.Collections.Generic;
    using System.Xml;
    using System.Xml.Linq;

    internal static class SubscriptionDescriptionExtensions
    {
        public static void NormalizeDescription(this SubscriptionDescription description, string baseAddress)
        {
            if (!string.IsNullOrWhiteSpace(description.ForwardTo))
            {
                description.ForwardTo = NormalizeForwardToAddress(description.ForwardTo, baseAddress);
            }

            if (!string.IsNullOrWhiteSpace(description.ForwardDeadLetteredMessagesTo))
            {
                description.ForwardDeadLetteredMessagesTo = NormalizeForwardToAddress(description.ForwardDeadLetteredMessagesTo, baseAddress);
            }
        }

        static string NormalizeForwardToAddress(string forwardTo, string baseAddress)
        {
            if (!Uri.TryCreate(forwardTo, UriKind.Absolute, out var forwardToUri))
            {
                if (!baseAddress.EndsWith("/", StringComparison.Ordinal))
                {
                    baseAddress += "/";
                }

                forwardToUri = new Uri(new Uri(baseAddress), forwardTo);
            }

            return forwardToUri.AbsoluteUri;
        }

        public static SubscriptionDescription ParseFromContent(string topicName, string xml)
        {
            var xDoc = XElement.Parse(xml);
            if (!xDoc.IsEmpty)
            {
                if (xDoc.Name.LocalName == "entry")
                {
                    return ParseFromEntryElement(topicName, xDoc);
                }
            }

            throw new MessagingEntityNotFoundException("Subscription was not found");
        }

        public static IList<SubscriptionDescription> ParseCollectionFromContent(string topicName, string xml)
        {
            var xDoc = XElement.Parse(xml);
            if (!xDoc.IsEmpty)
            {
                if (xDoc.Name.LocalName == "feed")
                {
                    var subscriptionList = new List<SubscriptionDescription>();

                    var entryList = xDoc.Elements(XName.Get("entry", ManagementClientConstants.AtomNs));
                    foreach (var entry in entryList)
                    {
                        subscriptionList.Add(ParseFromEntryElement(topicName, entry));
                    }

                    return subscriptionList;
                }
            }

            throw new MessagingEntityNotFoundException("Subscription was not found");
        }

        public static SubscriptionDescription ParseFromEntryElement(string topicName, XElement xEntry)
        {
            try
            {
                var name = xEntry.Element(XName.Get("title", ManagementClientConstants.AtomNs)).Value;
                var subscriptionDesc = new SubscriptionDescription(topicName, name);

                var qdXml = xEntry.Element(XName.Get("content", ManagementClientConstants.AtomNs))?
                    .Element(XName.Get("SubscriptionDescription", ManagementClientConstants.SbNs));

                if (qdXml == null)
                {
                    throw new MessagingEntityNotFoundException("Subscription was not found");
                }

                foreach (var element in qdXml.Elements())
                {
                    switch (element.Name.LocalName)
                    {
                        case "RequiresSession":
                            subscriptionDesc.RequiresSession = bool.Parse(element.Value);
                            break;
                        case "DeadLetteringOnMessageExpiration":
                            subscriptionDesc.EnableDeadLetteringOnMessageExpiration = bool.Parse(element.Value);
                            break;
                        case "DeadLetteringOnFilterEvaluationExceptions":
                            subscriptionDesc.EnableDeadLetteringOnFilterEvaluationExceptions = bool.Parse(element.Value);
                            break;
                        case "LockDuration":
                            subscriptionDesc.LockDuration = XmlConvert.ToTimeSpan(element.Value);
                            break;
                        case "DefaultMessageTimeToLive":
                            subscriptionDesc.DefaultMessageTimeToLive = XmlConvert.ToTimeSpan(element.Value);
                            break;
                        case "MaxDeliveryCount":
                            subscriptionDesc.MaxDeliveryCount = int.Parse(element.Value);
                            break;
                        case "Status":
                            subscriptionDesc.Status = (EntityStatus)Enum.Parse(typeof(EntityStatus), element.Value);
                            break;
                        case "EnableBatchedOperations":
                            subscriptionDesc.EnableBatchedOperations = bool.Parse(element.Value);
                            break;
                        case "UserMetadata":
                            subscriptionDesc.UserMetadata = element.Value;
                            break;
                        case "AutoDeleteOnIdle":
                            subscriptionDesc.AutoDeleteOnIdle = XmlConvert.ToTimeSpan(element.Value);
                            break;
                        case "ForwardTo":
                            if (!string.IsNullOrWhiteSpace(element.Value))
                            {
                                subscriptionDesc.ForwardTo = element.Value;
                            }
                            break;
                        case "ForwardDeadLetteredMessagesTo":
                            if (!string.IsNullOrWhiteSpace(element.Value))
                            {
                                subscriptionDesc.ForwardDeadLetteredMessagesTo = element.Value;
                            }
                            break;
                    }
                }

                return subscriptionDesc;
            }
            catch (Exception ex) when (!(ex is ServiceBusException))
            {
                throw new ServiceBusException(false, ex);
            }
        }

        public static XDocument Serialize(this SubscriptionDescription description)
        {
            return new XDocument(
                new XElement(XName.Get("entry", ManagementClientConstants.AtomNs),
                    new XElement(XName.Get("content", ManagementClientConstants.AtomNs),
                        new XAttribute("type", "application/xml"),
                        new XElement(XName.Get("SubscriptionDescription", ManagementClientConstants.SbNs),
                            new XElement(XName.Get("LockDuration", ManagementClientConstants.SbNs), XmlConvert.ToString(description.LockDuration)),
                            new XElement(XName.Get("RequiresSession", ManagementClientConstants.SbNs), XmlConvert.ToString(description.RequiresSession)),
                            description.DefaultMessageTimeToLive != TimeSpan.MaxValue ? new XElement(XName.Get("DefaultMessageTimeToLive", ManagementClientConstants.SbNs), XmlConvert.ToString(description.DefaultMessageTimeToLive)) : null,
                            new XElement(XName.Get("DeadLetteringOnMessageExpiration", ManagementClientConstants.SbNs), XmlConvert.ToString(description.EnableDeadLetteringOnMessageExpiration)),
                            new XElement(XName.Get("DeadLetteringOnFilterEvaluationExceptions", ManagementClientConstants.SbNs), XmlConvert.ToString(description.EnableDeadLetteringOnFilterEvaluationExceptions)),
                            description.DefaultRuleDescription != null ? description.DefaultRuleDescription.SerializeRule("DefaultRuleDescription") : null,
                            new XElement(XName.Get("MaxDeliveryCount", ManagementClientConstants.SbNs), XmlConvert.ToString(description.MaxDeliveryCount)),
                            new XElement(XName.Get("EnableBatchedOperations", ManagementClientConstants.SbNs), XmlConvert.ToString(description.EnableBatchedOperations)),
                            new XElement(XName.Get("Status", ManagementClientConstants.SbNs), description.Status.ToString()),
                            description.ForwardTo != null ? new XElement(XName.Get("ForwardTo", ManagementClientConstants.SbNs), description.ForwardTo) : null,
                            description.UserMetadata != null ? new XElement(XName.Get("UserMetadata", ManagementClientConstants.SbNs), description.UserMetadata) : null,
                            description.ForwardDeadLetteredMessagesTo != null ? new XElement(XName.Get("ForwardDeadLetteredMessagesTo", ManagementClientConstants.SbNs), description.ForwardDeadLetteredMessagesTo) : null,
                            description.AutoDeleteOnIdle != TimeSpan.MaxValue ? new XElement(XName.Get("AutoDeleteOnIdle", ManagementClientConstants.SbNs), XmlConvert.ToString(description.AutoDeleteOnIdle)) : null
                        ))
                ));
        }
    }
}