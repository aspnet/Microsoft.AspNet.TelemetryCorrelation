using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.AspNet.CorrelationActivity.Tests
{
    public class ActivityExtensionsTest
    {
        private const string TestActivityName = "Activity.Test";

        [Fact]
        public void Restore_Nothing_If_Header_Does_Not_Contain_RequestId()
        {
            var activity = new Activity(TestActivityName);
            var requestHeaders = new NameValueCollection();

            activity.RestoreActivityInfoFromRequestHeaders(requestHeaders);

            Assert.True(string.IsNullOrEmpty(activity.ParentId));
            Assert.Empty(activity.Baggage);
        }

        [Fact]
        public void Can_Restore_First_RequestId_When_Multiple_RequestId_In_Headers()
        {
            var activity = new Activity(TestActivityName);
            var requestHeaders = new NameValueCollection();
            requestHeaders.Add(ActivityExtensions.RequestIDHeaderName, "|aba2f1e978b11111.1");
            requestHeaders.Add(ActivityExtensions.RequestIDHeaderName, "|aba2f1e978b22222.1");

            activity.RestoreActivityInfoFromRequestHeaders(requestHeaders);

            Assert.Equal("|aba2f1e978b11111.1", activity.ParentId);
            Assert.Empty(activity.Baggage);
        }

        [Fact]
        public void Restore_Empty_RequestId_Should_Not_Throw_Exception()
        {
            var activity = new Activity(TestActivityName);
            var requestHeaders = new NameValueCollection();
            requestHeaders.Add(ActivityExtensions.RequestIDHeaderName, "");

            activity.RestoreActivityInfoFromRequestHeaders(requestHeaders);

            Assert.True(string.IsNullOrEmpty(activity.ParentId));
            Assert.Empty(activity.Baggage);
        }

        [Fact]
        public void Can_Restore_Baggages_When_CorrelationContext_In_Headers()
        {
            var activity = new Activity(TestActivityName);
            var requestHeaders = new NameValueCollection();
            requestHeaders.Add(ActivityExtensions.RequestIDHeaderName, "|aba2f1e978b11111.1");
            requestHeaders.Add(ActivityExtensions.CorrelationContextHeaderName, "key1=123,key2=456,key3=789");

            activity.RestoreActivityInfoFromRequestHeaders(requestHeaders);

            Assert.Equal("|aba2f1e978b11111.1", activity.ParentId);
            var baggageItems = new List<KeyValuePair<string, string>>();
            baggageItems.Add(new KeyValuePair<string, string>("key1", "123"));
            baggageItems.Add(new KeyValuePair<string, string>("key2", "456"));
            baggageItems.Add(new KeyValuePair<string, string>("key3", "789"));
            var expectedBaggage = baggageItems.OrderBy(kvp => kvp.Key);
            var actualBaggage = activity.Baggage.OrderBy(kvp => kvp.Key);
            Assert.Equal(expectedBaggage, actualBaggage);
        }

        [Fact]
        public void Can_Restore_Baggages_When_Multiple_CorrelationContext_In_Headers()
        {
            var activity = new Activity(TestActivityName);
            var requestHeaders = new NameValueCollection();
            requestHeaders.Add(ActivityExtensions.RequestIDHeaderName, "|aba2f1e978b11111.1");
            requestHeaders.Add(ActivityExtensions.CorrelationContextHeaderName, "key1=123,key2=456,key3=789");
            requestHeaders.Add(ActivityExtensions.CorrelationContextHeaderName, "key4=abc,key5=def");
            requestHeaders.Add(ActivityExtensions.CorrelationContextHeaderName, "key6=xyz");

            activity.RestoreActivityInfoFromRequestHeaders(requestHeaders);

            Assert.Equal("|aba2f1e978b11111.1", activity.ParentId);
            var baggageItems = new List<KeyValuePair<string, string>>();
            baggageItems.Add(new KeyValuePair<string, string>("key1", "123"));
            baggageItems.Add(new KeyValuePair<string, string>("key2", "456"));
            baggageItems.Add(new KeyValuePair<string, string>("key3", "789"));
            baggageItems.Add(new KeyValuePair<string, string>("key4", "abc"));
            baggageItems.Add(new KeyValuePair<string, string>("key5", "def"));
            baggageItems.Add(new KeyValuePair<string, string>("key6", "xyz"));
            var expectedBaggage = baggageItems.OrderBy(kvp => kvp.Key);
            var actualBaggage = activity.Baggage.OrderBy(kvp => kvp.Key);
            Assert.Equal(expectedBaggage, actualBaggage);
        }

        [Fact]
        public void Can_Restore_Baggages_When_Some_MalFormat_CorrelationContext_In_Headers()
        {
            var activity = new Activity(TestActivityName);
            var requestHeaders = new NameValueCollection();
            requestHeaders.Add(ActivityExtensions.RequestIDHeaderName, "|aba2f1e978b11111.1");
            requestHeaders.Add(ActivityExtensions.CorrelationContextHeaderName, "key1=123,key2=456,key3=789");
            requestHeaders.Add(ActivityExtensions.CorrelationContextHeaderName, "key4=abc;key5=def");
            requestHeaders.Add(ActivityExtensions.CorrelationContextHeaderName, "key6????xyz");

            activity.RestoreActivityInfoFromRequestHeaders(requestHeaders);

            Assert.Equal("|aba2f1e978b11111.1", activity.ParentId);
            var baggageItems = new List<KeyValuePair<string, string>>();
            baggageItems.Add(new KeyValuePair<string, string>("key1", "123"));
            baggageItems.Add(new KeyValuePair<string, string>("key2", "456"));
            baggageItems.Add(new KeyValuePair<string, string>("key3", "789"));
            var expectedBaggage = baggageItems.OrderBy(kvp => kvp.Key);
            var actualBaggage = activity.Baggage.OrderBy(kvp => kvp.Key);
            Assert.Equal(expectedBaggage, actualBaggage);
        }
    }
}
