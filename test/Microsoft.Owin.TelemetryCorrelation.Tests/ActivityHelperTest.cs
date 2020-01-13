// <copyright file="ActivityHelperTest.cs" company="Microsoft">
// Copyright (c) .NET Foundation. All rights reserved.
//
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
// </copyright>

namespace Microsoft.Owin.TelemetryCorrelation.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Microsoft.AspNet.TelemetryCorrelation;
    using Xunit;

    public class ActivityHelperTest : IDisposable
    {
        private const string TestActivityName = "Activity.Test";
        private readonly List<KeyValuePair<string, string>> baggageItems;
        private readonly string baggageInHeader;
        private IDisposable subscriptionAllListeners;
        private IDisposable subscriptionOwinListener;

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
            var owinListenerField = typeof(ActivityHelper).
                GetField("OwinListener", BindingFlags.Static | BindingFlags.NonPublic);
            owinListenerField.SetValue(null, new DiagnosticListener(ActivityHelper.OwinListenerName));
        }

        public void Dispose()
        {
            this.subscriptionOwinListener?.Dispose();
            this.subscriptionAllListeners?.Dispose();
        }

        [Fact]
        public void Should_Not_Create_RootActivity_If_OwinListener_Not_Enabled()
        {
            var context = CreateOwinContext();
            var rootActivity = ActivityHelper.CreateRootActivity(context);

            Assert.Null(rootActivity);
        }

        [Fact]
        public void Should_Not_Create_RootActivity_If_OwinActivity_Not_Enabled()
        {
            var context = CreateOwinContext();
            this.EnableOwinListenerOnly();
            var rootActivity = ActivityHelper.CreateRootActivity(context);

            Assert.Null(rootActivity);
        }

        [Fact]
        public void Should_Not_Create_RootActivity_If_AspNetActivity_Not_Enabled_With_Arguments()
        {
            var context = CreateOwinContext();
            this.EnableOwinListenerAndDisableActivity();
            var rootActivity = ActivityHelper.CreateRootActivity(context);

            Assert.Null(rootActivity);
        }

        [Fact]
        public void Can_Create_RootActivity_And_Restore_Info_From_Request_Header()
        {
            this.EnableAll();
            var requestHeaders = new Dictionary<string, string>
            {
                { ActivityExtensions.RequestIdHeaderName, "|aba2f1e978b2cab6.1." },
                { ActivityExtensions.CorrelationContextHeaderName, this.baggageInHeader }
            };

            var context = CreateOwinContext(headers: requestHeaders);
            this.EnableOwinListenerAndActivity();
            var rootActivity = ActivityHelper.CreateRootActivity(context);

            Assert.NotNull(rootActivity);
            Assert.True(rootActivity.ParentId == "|aba2f1e978b2cab6.1.");
            var expectedBaggage = this.baggageItems.OrderBy(item => item.Value);
            var actualBaggage = rootActivity.Baggage.OrderBy(item => item.Value);
            Assert.Equal(expectedBaggage, actualBaggage);
        }

        [Fact]
        public void Can_Create_RootActivity_From_W3C_Traceparent()
        {
            this.EnableAll();
            var requestHeaders = new Dictionary<string, string>
            {
                { ActivityExtensions.TraceparentHeaderName, "00-0123456789abcdef0123456789abcdef-0123456789abcdef-00" },
            };

            var context = CreateOwinContext(headers: requestHeaders);
            this.EnableOwinListenerAndActivity();
            var rootActivity = ActivityHelper.CreateRootActivity(context);

            Assert.NotNull(rootActivity);
            Assert.Equal(ActivityIdFormat.W3C, rootActivity.IdFormat);
            Assert.Equal("00-0123456789abcdef0123456789abcdef-0123456789abcdef-00", rootActivity.ParentId);
            Assert.Equal("0123456789abcdef0123456789abcdef", rootActivity.TraceId.ToHexString());
            Assert.Equal("0123456789abcdef", rootActivity.ParentSpanId.ToHexString());
            Assert.False(rootActivity.Recorded);

            Assert.Null(rootActivity.TraceStateString);
            Assert.Empty(rootActivity.Baggage);
        }

        [Fact]
        public void Can_Create_RootActivityWithTraceState_From_W3C_TraceContext()
        {
            this.EnableAll();
            var requestHeaders = new Dictionary<string, string>
            {
                { ActivityExtensions.TraceparentHeaderName, "00-0123456789abcdef0123456789abcdef-0123456789abcdef-01" },
                { ActivityExtensions.TracestateHeaderName, "ts1=v1,ts2=v2" },
            };

            var context = CreateOwinContext(headers: requestHeaders);
            this.EnableOwinListenerAndActivity();
            var rootActivity = ActivityHelper.CreateRootActivity(context);

            Assert.NotNull(rootActivity);
            Assert.Equal(ActivityIdFormat.W3C, rootActivity.IdFormat);
            Assert.Equal("00-0123456789abcdef0123456789abcdef-0123456789abcdef-01", rootActivity.ParentId);
            Assert.Equal("0123456789abcdef0123456789abcdef", rootActivity.TraceId.ToHexString());
            Assert.Equal("0123456789abcdef", rootActivity.ParentSpanId.ToHexString());
            Assert.True(rootActivity.Recorded);

            Assert.Equal("ts1=v1,ts2=v2", rootActivity.TraceStateString);
            Assert.Empty(rootActivity.Baggage);
        }

        [Fact]
        public void Can_Create_RootActivity_And_Start_Activity()
        {
            this.EnableAll();
            var context = CreateOwinContext();
            this.EnableOwinListenerAndActivity();
            var rootActivity = ActivityHelper.CreateRootActivity(context);

            Assert.NotNull(rootActivity);
            Assert.True(!string.IsNullOrEmpty(rootActivity.Id));
        }

        [Fact]
        public void Can_Create_RootActivity_And_Saved_In_OwinContext()
        {
            this.EnableAll();
            var context = CreateOwinContext();
            this.EnableOwinListenerAndActivity();
            var rootActivity = ActivityHelper.CreateRootActivity(context);

            Assert.NotNull(rootActivity);
            Assert.Same(rootActivity, context.Get<Activity>(ActivityHelper.ActivityKey));
        }

        [Fact]
        public async Task Can_Stop_Activity_Without_OwinListener_Enabled()
        {
            var context = CreateOwinContext();
            var rootActivity = this.CreateActivity();
            rootActivity.Start();
            context.Set(ActivityHelper.ActivityKey, rootActivity);
            await Task.Delay(100);
            ActivityHelper.StopOwinActivity(context);

            Assert.True(rootActivity.Duration != TimeSpan.Zero);
            Assert.Null(rootActivity.Parent);
            Assert.Null(context.Get<Activity>(ActivityHelper.ActivityKey));
        }

        [Fact]
        public async Task Can_Stop_Activity_With_OwinListener_Enabled()
        {
            var context = CreateOwinContext();
            var rootActivity = this.CreateActivity();
            rootActivity.Start();
            context.Set(ActivityHelper.ActivityKey, rootActivity);
            await Task.Delay(100);
            this.EnableOwinListenerOnly();
            ActivityHelper.StopOwinActivity(context);

            Assert.True(rootActivity.Duration != TimeSpan.Zero);
            Assert.Null(rootActivity.Parent);
            Assert.Null(context.Get<Activity>(ActivityHelper.ActivityKey));
        }

        [Fact]
        public void Can_Stop_Root_Activity_With_All_Children()
        {
            this.EnableAll();
            var context = CreateOwinContext();
            var rootActivity = ActivityHelper.CreateRootActivity(context);

            var child = new Activity("child").Start();
            new Activity("grandchild").Start();

            ActivityHelper.StopOwinActivity(context);

            Assert.True(rootActivity.Duration != TimeSpan.Zero);
            Assert.True(child.Duration == TimeSpan.Zero);
            Assert.Null(rootActivity.Parent);
            Assert.Null(context.Get<Activity>(ActivityHelper.ActivityKey));
        }

        [Fact]
        public void Can_Stop_Root_While_Child_Is_Current()
        {
            this.EnableAll();
            var context = CreateOwinContext();
            ActivityHelper.CreateRootActivity(context);
            var child = new Activity("child").Start();

            ActivityHelper.StopOwinActivity(context);

            Assert.True(child.Duration == TimeSpan.Zero);
            Assert.Null(Activity.Current);
            Assert.Null(context.Get<Activity>(ActivityHelper.ActivityKey));
        }

        [Fact]
        public async Task Can_Stop_Root_Activity_If_It_Is_Broken()
        {
            this.EnableAll();
            var context = CreateOwinContext();
            var root = ActivityHelper.CreateRootActivity(context);
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
            ActivityHelper.StopOwinActivity(context);
            Assert.True(root.Duration != TimeSpan.Zero);
            Assert.Null(context.Get<Activity>(ActivityHelper.ActivityKey));
            Assert.Null(Activity.Current);
        }

        [Fact]
        public void Stop_Root_Activity_With_129_Nesting_Depth()
        {
            this.EnableAll();
            var context = CreateOwinContext();
            var root = ActivityHelper.CreateRootActivity(context);

            for (int i = 0; i < 129; i++)
            {
                new Activity("child" + i).Start();
            }

            // can stop any activity regardless of the stack depth
            ActivityHelper.StopOwinActivity(context);

            Assert.True(root.Duration != TimeSpan.Zero);
            Assert.Null(context.Get<Activity>(ActivityHelper.ActivityKey));
            Assert.Null(Activity.Current);
        }

        [Fact]
        public void Can_Stop_Activity_And_Pass_Exception_As_A_Payload()
        {
            object exception = null;
            this.EnableAll(onNext: kvp => { exception = GetProperty(kvp.Value, "Exception"); });
            var context = CreateOwinContext();
            ActivityHelper.CreateRootActivity(context);
            ActivityHelper.StopOwinActivity(context, new ArgumentException());

            Assert.NotNull(exception);
            Assert.IsType<ArgumentException>(exception);
        }

        private static IOwinContext CreateOwinContext(string page = "/page", string query = "", IDictionary<string, string> headers = null)
        {
            var context = new OwinContext
            {
                Request =
                {
                    Method = "GET",
                    Scheme = "https",
                    Host = new HostString("example.com"),
                    Path = new PathString(page),
                    QueryString = new QueryString(query)
                }
            };

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    context.Request.Headers.Append(header.Key, header.Value);
                }
            }

            return context;
        }

        private static object GetProperty(object obj, string propertyName)
        {
            return obj.GetType().GetTypeInfo().GetDeclaredProperty(propertyName)?.GetValue(obj);
        }

        private Activity CreateActivity()
        {
            var activity = new Activity(TestActivityName);
            this.baggageItems.ForEach(kv => activity.AddBaggage(kv.Key, kv.Value));

            return activity;
        }

        private void EnableOwinListenerOnly(Action<KeyValuePair<string, object>> onNext = null)
        {
            this.subscriptionAllListeners = DiagnosticListener.AllListeners.Subscribe(listener =>
            {
                // if OwinListener has subscription, then it is enabled
                if (listener.Name == ActivityHelper.OwinListenerName)
                {
                    this.subscriptionOwinListener = listener.Subscribe(
                        new TestDiagnosticListener(onNext),
                        activityName => false);
                }
            });
        }

        private void EnableOwinListenerAndDisableActivity(
            Action<KeyValuePair<string, object>> onNext = null,
            string activityName = ActivityHelper.OwinActivityName)
        {
            this.subscriptionAllListeners = DiagnosticListener.AllListeners.Subscribe(listener =>
            {
                // if OwinListener has subscription, then it is enabled
                if (listener.Name == ActivityHelper.OwinListenerName)
                {
                    this.subscriptionOwinListener = listener.Subscribe(
                        new TestDiagnosticListener(onNext),
                        (name, arg1, arg2) => name == activityName && arg1 == null);
                }
            });
        }

        private void EnableOwinListenerAndActivity(
            Action<KeyValuePair<string, object>> onNext = null,
            string activityName = ActivityHelper.OwinActivityName)
        {
            this.subscriptionAllListeners = DiagnosticListener.AllListeners.Subscribe(listener =>
            {
                // if OwinListener has subscription, then it is enabled
                if (listener.Name == ActivityHelper.OwinListenerName)
                {
                    this.subscriptionOwinListener = listener.Subscribe(
                        new TestDiagnosticListener(onNext),
                        (name, arg1, arg2) => name == activityName);
                }
            });
        }

        private void EnableAll(
            Action<KeyValuePair<string, object>> onNext = null,
            Action<Activity, object> onImport = null)
        {
            this.subscriptionAllListeners = DiagnosticListener.AllListeners.Subscribe(listener =>
            {
                // if OwinListener has subscription, then it is enabled
                if (listener.Name == ActivityHelper.OwinListenerName)
                {
                    this.subscriptionOwinListener = listener.Subscribe(
                        new TestDiagnosticListener(onNext),
                        (name, a1, a2) => true,
                        (a, o) => onImport?.Invoke(a, o),
                        (a, o) => { });
                }
            });
        }
    }
}