// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using Xunit;

namespace Microsoft.AspNet.TelemetryCorrelation.Tests
{
    public class ActivityExtensionsTest
    {
        private const string TestActivityName = "Activity.Test";

        [Fact]
        public void Restore_Nothing_If_Header_Does_Not_Contain_RequestId()
        {
            var activity = new Activity(TestActivityName);
            var requestHeaders = new NameValueCollection();

            Assert.False(activity.Extract(requestHeaders));

            Assert.True(string.IsNullOrEmpty(activity.ParentId));
            Assert.Empty(activity.Baggage);
        }

        [Fact]
        public void Can_Restore_First_RequestId_When_Multiple_RequestId_In_Headers()
        {
            var activity = new Activity(TestActivityName);
            var requestHeaders = new NameValueCollection
            {
                { ActivityExtensions.RequestIDHeaderName, "|aba2f1e978b11111.1" },
                { ActivityExtensions.RequestIDHeaderName, "|aba2f1e978b22222.1" }
            };
            Assert.True(activity.Extract(requestHeaders));

            Assert.Equal("|aba2f1e978b11111.1", activity.ParentId);
            Assert.Empty(activity.Baggage);
        }

        [Fact]
        public void Restore_Empty_RequestId_Should_Not_Throw_Exception()
        {
            var activity = new Activity(TestActivityName);
            var requestHeaders = new NameValueCollection
            {
                { ActivityExtensions.RequestIDHeaderName, "" }
            };
            Assert.False(activity.Extract(requestHeaders));

            Assert.Null(activity.ParentId);
            Assert.Empty(activity.Baggage);
        }

        [Fact]
        public void Can_Restore_Baggages_When_CorrelationContext_In_Headers()
        {
            var activity = new Activity(TestActivityName);
            var requestHeaders = new NameValueCollection
            {
                { ActivityExtensions.RequestIDHeaderName, "|aba2f1e978b11111.1" },
                { ActivityExtensions.CorrelationContextHeaderName, "key1=123,key2=456,key3=789" }
            };
            Assert.True(activity.Extract(requestHeaders));

            Assert.Equal("|aba2f1e978b11111.1", activity.ParentId);
            var baggageItems = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("key1", "123"),
                new KeyValuePair<string, string>("key2", "456"),
                new KeyValuePair<string, string>("key3", "789")
            };
            var expectedBaggage = baggageItems.OrderBy(kvp => kvp.Key);
            var actualBaggage = activity.Baggage.OrderBy(kvp => kvp.Key);
            Assert.Equal(expectedBaggage, actualBaggage);
        }

        [Fact]
        public void Can_Restore_Baggages_When_Multiple_CorrelationContext_In_Headers()
        {
            var activity = new Activity(TestActivityName);
            var requestHeaders = new NameValueCollection
            {
                { ActivityExtensions.RequestIDHeaderName, "|aba2f1e978b11111.1" },
                { ActivityExtensions.CorrelationContextHeaderName, "key1=123,key2=456,key3=789" },
                { ActivityExtensions.CorrelationContextHeaderName, "key4=abc,key5=def" },
                { ActivityExtensions.CorrelationContextHeaderName, "key6=xyz" }
            };
            Assert.True(activity.Extract(requestHeaders));

            Assert.Equal("|aba2f1e978b11111.1", activity.ParentId);
            var baggageItems = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("key1", "123"),
                new KeyValuePair<string, string>("key2", "456"),
                new KeyValuePair<string, string>("key3", "789"),
                new KeyValuePair<string, string>("key4", "abc"),
                new KeyValuePair<string, string>("key5", "def"),
                new KeyValuePair<string, string>("key6", "xyz")
            };
            var expectedBaggage = baggageItems.OrderBy(kvp => kvp.Key);
            var actualBaggage = activity.Baggage.OrderBy(kvp => kvp.Key);
            Assert.Equal(expectedBaggage, actualBaggage);
        }

        [Fact]
        public void Can_Restore_Baggages_When_Some_MalFormat_CorrelationContext_In_Headers()
        {
            var activity = new Activity(TestActivityName);
            var requestHeaders = new NameValueCollection
            {
                { ActivityExtensions.RequestIDHeaderName, "|aba2f1e978b11111.1" },
                { ActivityExtensions.CorrelationContextHeaderName, "key1=123,key2=456,key3=789" },
                { ActivityExtensions.CorrelationContextHeaderName, "key4=abc;key5=def" },
                { ActivityExtensions.CorrelationContextHeaderName, "key6????xyz" },
                { ActivityExtensions.CorrelationContextHeaderName, "key7=123=456" }
            };
            Assert.True(activity.Extract(requestHeaders));

            Assert.Equal("|aba2f1e978b11111.1", activity.ParentId);
            var baggageItems = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("key1", "123"),
                new KeyValuePair<string, string>("key2", "456"),
                new KeyValuePair<string, string>("key3", "789")
            };
            var expectedBaggage = baggageItems.OrderBy(kvp => kvp.Key);
            var actualBaggage = activity.Baggage.OrderBy(kvp => kvp.Key);
            Assert.Equal(expectedBaggage, actualBaggage);
        }
    }
}
