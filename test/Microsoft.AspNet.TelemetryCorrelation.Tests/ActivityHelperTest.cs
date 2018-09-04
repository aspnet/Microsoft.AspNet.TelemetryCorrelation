// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.AspNet.TelemetryCorrelation.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;
    using Xunit;

    public class ActivityHelperTest : IDisposable
    {
        private const string TestActivityName = "Activity.Test";
        private readonly List<KeyValuePair<string, string>> baggageItems;
        private readonly string baggageInHeader;
        private IDisposable subscriptionAllListeners;
        private IDisposable subscriptionAspNetListener;

        public ActivityHelperTest()
        {
            this.baggageItems = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("TestKey1", "123"),
                new KeyValuePair<string, string>("TestKey2", "456"),
                new KeyValuePair<string, string>("TestKey1", "789")
            };

            this.baggageInHeader = "TestKey1=123,TestKey2=456,TestKey1=789";

            // reset static fields
            var allListenerField = typeof(DiagnosticListener).
                GetField("s_allListenerObservable", BindingFlags.Static | BindingFlags.NonPublic);
            allListenerField.SetValue(null, null);
            var aspnetListenerField = typeof(ActivityHelper).
                GetField("AspNetListener", BindingFlags.Static | BindingFlags.NonPublic);
            aspnetListenerField.SetValue(null, new DiagnosticListener(ActivityHelper.AspNetListenerName));
        }

        public void Dispose()
        {
            this.subscriptionAspNetListener?.Dispose();
            this.subscriptionAllListeners?.Dispose();
        }

        [Fact]
        public async Task Can_Restore_Activity()
        {
            var rootActivity = this.CreateActivity();

            rootActivity.AddTag("k1", "v1");
            rootActivity.AddTag("k2", "v2");
            var context = HttpContextHelper.GetFakeHttpContext();
            await Task.Run(() =>
            {
                rootActivity.Start();
                context.Items[ActivityHelper.ActivityKey] = rootActivity;
            });
            Assert.Null(Activity.Current);

            ActivityHelper.RestoreActivityIfNeeded(context.Items);

            this.AssertIsRestoredActivity(rootActivity, Activity.Current);
        }

        [Fact]
        public void Do_Not_Restore_Activity_When_There_Is_No_Activity_In_Context()
        {
            ActivityHelper.RestoreActivityIfNeeded(HttpContextHelper.GetFakeHttpContext().Items);

            Assert.Null(Activity.Current);
        }

        [Fact]
        public void Do_Not_Restore_Activity_When_It_Is_Not_Lost()
        {
            var root = new Activity("root").Start();

            var context = HttpContextHelper.GetFakeHttpContext();
            context.Items[ActivityHelper.ActivityKey] = root;

            var module = new TelemetryCorrelationHttpModule();

            ActivityHelper.RestoreActivityIfNeeded(context.Items);

            Assert.Equal(root, Activity.Current);
        }

        [Fact]
        public async Task Stop_Restored_Activity_Deletes_It_From_Items()
        {
            var context = HttpContextHelper.GetFakeHttpContext();
            var root = new Activity("root");

            await Task.Run(() =>
            {
                root.Start();
                context.Items[ActivityHelper.ActivityKey] = root;
            });

            ActivityHelper.RestoreActivityIfNeeded(context.Items);

            var child = Activity.Current;

            ActivityHelper.StopRestoredActivity(child, context);
            Assert.NotNull(context.Items[ActivityHelper.ActivityKey]);
            Assert.Null(context.Items[ActivityHelper.RestoredActivityKey]);
        }

        [Fact]
        public async Task Stop_Restored_Activity_Fires_Event()
        {
            var context = HttpContextHelper.GetFakeHttpContext();
            var root = new Activity("root");

            await Task.Run(() =>
            {
                root.Start();
                context.Items[ActivityHelper.ActivityKey] = root;
            });

            ActivityHelper.RestoreActivityIfNeeded(context.Items);
            Activity restored = Activity.Current;

            var events = new ConcurrentQueue<KeyValuePair<string, object>>();
            this.EnableAll((kvp) => events.Enqueue(kvp));

            ActivityHelper.StopRestoredActivity(restored, context);

            Assert.Single(events);
            string eventName = events.Single().Key;
            object eventPayload = events.Single().Value;

            Assert.Equal(ActivityHelper.AspNetActivityRestoredStopName, eventName);
            Assert.Same(restored, eventPayload.GetProperty("Activity"));
        }

        [Fact]
        public void Can_Stop_Activity_Without_AspNetListener_Enabled()
        {
            var context = HttpContextHelper.GetFakeHttpContext();
            var rootActivity = this.CreateActivity();
            rootActivity.Start();
            Thread.Sleep(100);
            ActivityHelper.StopAspNetActivity(rootActivity, context.Items);

            Assert.True(rootActivity.Duration != TimeSpan.Zero);
            Assert.Null(rootActivity.Parent);
            Assert.Null(context.Items[ActivityHelper.ActivityKey]);
        }

        [Fact]
        public void Can_Stop_Activity_With_AspNetListener_Enabled()
        {
            var context = HttpContextHelper.GetFakeHttpContext();
            var rootActivity = this.CreateActivity();
            rootActivity.Start();
            Thread.Sleep(100);
            this.EnableAspNetListenerOnly();
            ActivityHelper.StopAspNetActivity(rootActivity, context.Items);

            Assert.True(rootActivity.Duration != TimeSpan.Zero);
            Assert.Null(rootActivity.Parent);
            Assert.Null(context.Items[ActivityHelper.ActivityKey]);
        }

        [Fact]
        public void Can_Stop_Root_Activity_With_All_Children()
        {
            var context = HttpContextHelper.GetFakeHttpContext();
            var rootActivity = this.CreateActivity();
            rootActivity.Start();
            new Activity("child").Start();
            new Activity("grandchild").Start();

            ActivityHelper.StopAspNetActivity(rootActivity, context.Items);

            Assert.True(rootActivity.Duration != TimeSpan.Zero);
            Assert.Null(rootActivity.Parent);
            Assert.Null(context.Items[ActivityHelper.ActivityKey]);
        }

        [Fact]
        public void Can_Stop_Child_Activity_With_All_Children()
        {
            var context = HttpContextHelper.GetFakeHttpContext();
            var rootActivity = this.CreateActivity();
            rootActivity.Start();
            var child = new Activity("child").Start();
            new Activity("grandchild").Start();

            ActivityHelper.StopAspNetActivity(child, context.Items);

            Assert.True(child.Duration != TimeSpan.Zero);
            Assert.Equal(rootActivity, Activity.Current);
            Assert.Null(context.Items[ActivityHelper.ActivityKey]);
        }

        [Fact]
        public async Task Can_Stop_Root_Activity_If_It_Is_Broken()
        {
            var context = HttpContextHelper.GetFakeHttpContext();
            var root = new Activity("root").Start();
            context.Items[ActivityHelper.ActivityKey] = root;
            new Activity("child").Start();

            for (int i = 0; i < 2; i++)
            {
                await Task.Run(() =>
                {
                    // when we enter this method, Current is 'child' activity
                    Activity.Current.Stop();

                    // here Current is 'parent', but only in this execution context
                });
            }

            // when we return back here, in the 'parent' execution context
            // Current is still 'child' activity - changes in child context (inside Task.Run)
            // do not affect 'parent' context in which Task.Run is called.
            // But 'child' Activity is stopped, thus consequent calls to Stop will
            // not update Current
            Assert.False(ActivityHelper.StopAspNetActivity(root, context.Items));
            Assert.NotNull(context.Items[ActivityHelper.ActivityKey]);
            Assert.Null(Activity.Current);
        }

        [Fact]
        public void Stop_Root_Activity_With_129_Nesting_Depth()
        {
            var context = HttpContextHelper.GetFakeHttpContext();
            var root = new Activity("root").Start();
            context.Items[ActivityHelper.ActivityKey] = root;

            for (int i = 0; i < 129; i++)
            {
                new Activity("child" + i).Start();
            }

            // we do not allow more than 128 nested activities here
            // only to protect from hypothetical cycles in Activity stack
            Assert.False(ActivityHelper.StopAspNetActivity(root, context.Items));

            Assert.NotNull(context.Items[ActivityHelper.ActivityKey]);
            Assert.Null(Activity.Current);
        }

        [Fact]
        public void Should_Not_Create_RootActivity_If_AspNetListener_Not_Enabled()
        {
            var context = HttpContextHelper.GetFakeHttpContext();
            var rootActivity = ActivityHelper.CreateRootActivity(context);

            Assert.Null(rootActivity);
        }

        [Fact]
        public void Should_Not_Create_RootActivity_If_AspNetActivity_Not_Enabled()
        {
            var context = HttpContextHelper.GetFakeHttpContext();
            this.EnableAspNetListenerOnly();
            var rootActivity = ActivityHelper.CreateRootActivity(context);

            Assert.Null(rootActivity);
        }

        [Fact]
        public void Should_Not_Create_RootActivity_If_AspNetActivity_Not_Enabled_With_Arguments()
        {
            var context = HttpContextHelper.GetFakeHttpContext();
            this.EnableAspNetListenerAndDisableActivity();
            var rootActivity = ActivityHelper.CreateRootActivity(context);

            Assert.Null(rootActivity);
        }

        [Fact]
        public void Can_Create_RootActivity_And_Restore_Info_From_Request_Header()
        {
            var requestHeaders = new Dictionary<string, string>
            {
                { ActivityExtensions.RequestIDHeaderName, "|aba2f1e978b2cab6.1" },
                { ActivityExtensions.CorrelationContextHeaderName, this.baggageInHeader }
            };

            var context = HttpContextHelper.GetFakeHttpContext(headers: requestHeaders);
            this.EnableAspNetListenerAndActivity();
            var rootActivity = ActivityHelper.CreateRootActivity(context);

            Assert.NotNull(rootActivity);
            Assert.True(rootActivity.ParentId == "|aba2f1e978b2cab6.1");
            var expectedBaggage = this.baggageItems.OrderBy(item => item.Value);
            var actualBaggage = rootActivity.Baggage.OrderBy(item => item.Value);
            Assert.Equal(expectedBaggage, actualBaggage);
        }

        [Fact]
        public void Can_Create_RootActivity_And_Start_Activity()
        {
            var context = HttpContextHelper.GetFakeHttpContext();
            this.EnableAspNetListenerAndActivity();
            var rootActivity = ActivityHelper.CreateRootActivity(context);

            Assert.NotNull(rootActivity);
            Assert.True(!string.IsNullOrEmpty(rootActivity.Id));
        }

        [Fact]
        public void Can_Create_RootActivity_And_Saved_In_HttContext()
        {
            var context = HttpContextHelper.GetFakeHttpContext();
            this.EnableAspNetListenerAndActivity();
            var rootActivity = ActivityHelper.CreateRootActivity(context);

            Assert.NotNull(rootActivity);
            Assert.Same(rootActivity, context.Items[ActivityHelper.ActivityKey]);
        }

        private void AssertIsRestoredActivity(Activity original, Activity restored)
        {
            Assert.NotNull(restored);
            Assert.Equal(original.RootId, restored.RootId);
            Assert.Equal(original.Id, restored.ParentId);
            Assert.Equal(original.StartTimeUtc, restored.StartTimeUtc);
            Assert.False(string.IsNullOrEmpty(restored.Id));
            var expectedBaggage = original.Baggage.OrderBy(item => item.Value);
            var actualBaggage = restored.Baggage.OrderBy(item => item.Value);
            Assert.Equal(expectedBaggage, actualBaggage);

            var expectedTags = original.Tags.OrderBy(item => item.Value);
            var actualTags = restored.Tags.OrderBy(item => item.Value);
            Assert.Equal(expectedTags, actualTags);
        }

        private Activity CreateActivity()
        {
            var activity = new Activity(TestActivityName);
            this.baggageItems.ForEach(kv => activity.AddBaggage(kv.Key, kv.Value));

            return activity;
        }

        private void EnableAll(Action<KeyValuePair<string, object>> onNext = null)
        {
            this.subscriptionAllListeners = DiagnosticListener.AllListeners.Subscribe(listener =>
            {
                // if AspNetListener has subscription, then it is enabled
                if (listener.Name == ActivityHelper.AspNetListenerName)
                {
                    this.subscriptionAspNetListener = listener.Subscribe(new TestDiagnosticListener(onNext), (name) => true);
                }
            });
        }

        private void EnableAspNetListenerAndDisableActivity(
            Action<KeyValuePair<string, object>> onNext = null,
            string activityName = ActivityHelper.AspNetActivityName)
        {
            this.subscriptionAllListeners = DiagnosticListener.AllListeners.Subscribe(listener =>
            {
                // if AspNetListener has subscription, then it is enabled
                if (listener.Name == ActivityHelper.AspNetListenerName)
                {
                    this.subscriptionAspNetListener = listener.Subscribe(
                        new TestDiagnosticListener(onNext),
                        (name, arg1, arg2) => name == activityName && arg1 == null);
                }
            });
        }

        private void EnableAspNetListenerAndActivity(
            Action<KeyValuePair<string, object>> onNext = null,
            string activityName = ActivityHelper.AspNetActivityName)
        {
            this.subscriptionAllListeners = DiagnosticListener.AllListeners.Subscribe(listener =>
            {
                // if AspNetListener has subscription, then it is enabled
                if (listener.Name == ActivityHelper.AspNetListenerName)
                {
                    this.subscriptionAspNetListener = listener.Subscribe(
                        new TestDiagnosticListener(onNext),
                        (name, arg1, arg2) => name == activityName);
                }
            });
        }

        private void EnableAspNetListenerOnly(Action<KeyValuePair<string, object>> onNext = null)
        {
            this.subscriptionAllListeners = DiagnosticListener.AllListeners.Subscribe(listener =>
            {
                // if AspNetListener has subscription, then it is enabled
                if (listener.Name == ActivityHelper.AspNetListenerName)
                {
                    this.subscriptionAspNetListener = listener.Subscribe(
                        new TestDiagnosticListener(onNext),
                        activityName => false);
                }
            });
        }

        private class TestHttpRequest : HttpRequestBase
        {
            private readonly NameValueCollection headers = new NameValueCollection();

            public override NameValueCollection Headers => this.headers;

            public override UnvalidatedRequestValuesBase Unvalidated => new TestUnvalidatedRequestValues(this.headers);
        }

        private class TestUnvalidatedRequestValues : UnvalidatedRequestValuesBase
        {
            public TestUnvalidatedRequestValues(NameValueCollection headers)
            {
                this.Headers = headers;
            }

            public override NameValueCollection Headers { get; }
        }

        private class TestHttpResponse : HttpResponseBase
        {
        }

        private class TestHttpServerUtility : HttpServerUtilityBase
        {
            private readonly HttpContextBase context;

            public TestHttpServerUtility(HttpContextBase context)
            {
                this.context = context;
            }

            public override Exception GetLastError()
            {
                return this.context.Error;
            }
        }

        private class TestHttpContext : HttpContextBase
        {
            private readonly Hashtable items;

            public TestHttpContext(Exception error = null)
            {
                this.Server = new TestHttpServerUtility(this);
                this.items = new Hashtable();
                this.Error = error;
            }

            public override HttpRequestBase Request { get; } = new TestHttpRequest();

            /// <inheritdoc />
            public override IDictionary Items => this.items;

            public override Exception Error { get; }

            public override HttpServerUtilityBase Server { get; }
        }
    }
}
