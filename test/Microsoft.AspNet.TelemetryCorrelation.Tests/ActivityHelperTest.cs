// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Xunit;

namespace Microsoft.AspNet.TelemetryCorrelation.Tests
{
    public class ActivityHelperTest
    {
        private const string TestActivityName = "Activity.Test";
        private readonly List<KeyValuePair<string, string>> _baggageItems;
        private readonly string _baggageInHeader;

        public ActivityHelperTest()
        {
            _baggageItems = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("TestKey1", "123"),
                new KeyValuePair<string, string>("TestKey2", "456"),
                new KeyValuePair<string, string>("TestKey1", "789")
            };

            _baggageInHeader = "TestKey1=123,TestKey2=456,TestKey1=789";
            // reset static fields
            var allListenerField = typeof(DiagnosticListener).
                GetField("s_allListenerObservable", BindingFlags.Static | BindingFlags.NonPublic);
            allListenerField.SetValue(null, null);
            var aspnetListenerField = typeof(ActivityHelper).
                GetField("AspNetListener", BindingFlags.Static | BindingFlags.NonPublic);
            aspnetListenerField.SetValue(null, new DiagnosticListener(ActivityHelper.AspNetListenerName));
        }                

        #region RestoreCurrentActivity tests
        [Fact]
        public async Task Can_Restore_Activity()
        {
            var rootActivity = CreateActivity();
            var context = HttpContextHelper.GetFakeHttpContext();
            await Task.Run(() =>
            {
                rootActivity.Start();
                context.Items[ActivityHelper.ActivityKey] = rootActivity;
            });
            Assert.Null(Activity.Current);

            var restoredActivity = ActivityHelper.RestoreCurrentActivity(rootActivity);

            Assert.NotNull(restoredActivity);
            Assert.True(rootActivity.Id == restoredActivity.ParentId);
            Assert.True(!string.IsNullOrEmpty(restoredActivity.Id));
            var expectedBaggage = _baggageItems.OrderBy(item => item.Value);
            var actualBaggage = rootActivity.Baggage.OrderBy(item => item.Value);
            Assert.Equal(expectedBaggage, actualBaggage);
        }
        #endregion

        #region StopAspNetActivity tests
        [Fact]
        public void Can_Stop_Activity_Without_AspNetListener_Enabled()
        {
            var context = HttpContextHelper.GetFakeHttpContext();
            var rootActivity = CreateActivity();
            rootActivity.Start();
            Thread.Sleep(100);
            ActivityHelper.StopAspNetActivity(rootActivity, context);

            Assert.True(rootActivity.Duration != TimeSpan.Zero);
            Assert.Null(rootActivity.Parent);
            Assert.Null(context.Items[ActivityHelper.ActivityKey]);
        }

        [Fact]
        public void Can_Stop_Activity_With_AspNetListener_Enabled()
        {
            var context = HttpContextHelper.GetFakeHttpContext();
            var rootActivity = CreateActivity();
            rootActivity.Start();
            Thread.Sleep(100);
            EnableAspNetListenerOnly();
            ActivityHelper.StopAspNetActivity(rootActivity, context);

            Assert.True(rootActivity.Duration != TimeSpan.Zero);
            Assert.Null(rootActivity.Parent);
            Assert.Null(context.Items[ActivityHelper.ActivityKey]);
        }


        [Fact]
        public void Can_Stop_Root_Activity_With_All_Children()
        {
            var context = HttpContextHelper.GetFakeHttpContext();
            var rootActivity = CreateActivity();
            rootActivity.Start();
            new Activity("child").Start();
            new Activity("grandchild").Start();

            ActivityHelper.StopAspNetActivity(rootActivity, context);

            Assert.True(rootActivity.Duration != TimeSpan.Zero);
            Assert.Null(rootActivity.Parent);
            Assert.Null(context.Items[ActivityHelper.ActivityKey]);
        }

        [Fact]
        public void Can_Stop_Child_Activity_With_All_Children()
        {
            var context = HttpContextHelper.GetFakeHttpContext();
            var rootActivity = CreateActivity();
            rootActivity.Start();
            var child = new Activity("child").Start();
            new Activity("grandchild").Start();

            ActivityHelper.StopAspNetActivity(child, context);

            Assert.True(child.Duration != TimeSpan.Zero);
            Assert.Equal(rootActivity, Activity.Current);
            Assert.Null(context.Items[ActivityHelper.ActivityKey]);
        }

        [Fact]
        public async Task Can_Stop_Root_Activity_If_It_Is_Broken()
        {
            var context = HttpContextHelper.GetFakeHttpContext();
            var root = new Activity("root").Start();
            ActivityHelper.SaveCurrentActivity(context, root);

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
            Assert.False(ActivityHelper.StopAspNetActivity(root, context));
            Assert.NotNull(context.Items[ActivityHelper.ActivityKey]);
            Assert.Null(Activity.Current);
        }

        [Fact]
        public void Stop_Root_Activity_With_129_Nesting_Depth()
        {
            var context = HttpContextHelper.GetFakeHttpContext();
            var root = new Activity("root");
            root.Start();
            ActivityHelper.SaveCurrentActivity(context, root);

            for (int i = 0; i < 129; i++)
            {
                new Activity("child" + i).Start();
            }

            // we do not allow more than 128 nested activities here 
            // only to protect from hypothetical cycles in Activity stack
            Assert.False(ActivityHelper.StopAspNetActivity(root, context));

            Assert.NotNull(context.Items[ActivityHelper.ActivityKey]);
            Assert.Null(Activity.Current);
        }

        #endregion

        #region CreateRootActivity tests
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
            EnableAspNetListenerOnly();
            var rootActivity = ActivityHelper.CreateRootActivity(context);

            Assert.Null(rootActivity);
        }

        [Fact]
        public void Should_Not_Create_RootActivity_If_AspNetActivity_Not_Enabled_With_Arguments()
        {
            var context = HttpContextHelper.GetFakeHttpContext();
            EnableAspNetListenerAndDisableActivity();
            var rootActivity = ActivityHelper.CreateRootActivity(context);

            Assert.Null(rootActivity);
        }

        [Fact]
        public void Can_Create_RootActivity_And_Restore_Info_From_Request_Header()
        {
            var requestHeaders = new Dictionary<string, string>
            {
                {ActivityExtensions.RequestIDHeaderName, "|aba2f1e978b2cab6.1"},
                {ActivityExtensions.CorrelationContextHeaderName, _baggageInHeader}
            };

            var context = HttpContextHelper.GetFakeHttpContext(headers: requestHeaders);
            EnableAspNetListenerAndActivity();
            var rootActivity = ActivityHelper.CreateRootActivity(context);

            Assert.NotNull(rootActivity);
            Assert.True(rootActivity.ParentId == "|aba2f1e978b2cab6.1");
            var expectedBaggage = _baggageItems.OrderBy(item => item.Value);
            var actualBaggage = rootActivity.Baggage.OrderBy(item => item.Value);
            Assert.Equal(expectedBaggage, actualBaggage);
        }

        [Fact]
        public void Can_Create_RootActivity_And_Start_Activity()
        {
            var context = HttpContextHelper.GetFakeHttpContext();
            EnableAspNetListenerAndActivity();
            var rootActivity = ActivityHelper.CreateRootActivity(context);

            Assert.NotNull(rootActivity);
            Assert.True(!string.IsNullOrEmpty(rootActivity.Id));
        }

        [Fact]
        public void Can_Create_RootActivity_And_Saved_In_HttContext()
        {
            var context = HttpContextHelper.GetFakeHttpContext();
            EnableAspNetListenerAndActivity();
            var rootActivity = ActivityHelper.CreateRootActivity(context);

            Assert.NotNull(rootActivity);
            Assert.Same(rootActivity, context.Items[ActivityHelper.ActivityKey]);
        }
        #endregion

        #region Helper methods               
        private Activity CreateActivity()
        {
            var activity = new Activity(TestActivityName);
            _baggageItems.ForEach(kv => activity.AddBaggage(kv.Key, kv.Value));

            return activity;
        }

        private void EnableAspNetListenerAndDisableActivity(Action<KeyValuePair<string, object>> onNext = null,
            string ActivityName = ActivityHelper.AspNetActivityName)
        {
            DiagnosticListener.AllListeners.Subscribe(listener =>
            {
                // if AspNetListener has subscription, then it is enabled
                if (listener.Name == ActivityHelper.AspNetListenerName)
                {
                    listener.Subscribe(new TestDiagnosticListener(onNext),
                        (name, arg1, arg2) => name == ActivityName && arg1 == null);
                }
            });
        }

        private void EnableAspNetListenerAndActivity(Action<KeyValuePair<string, object>> onNext = null, 
            string ActivityName = ActivityHelper.AspNetActivityName)
        {
            DiagnosticListener.AllListeners.Subscribe(listener =>
            {
                // if AspNetListener has subscription, then it is enabled
                if (listener.Name == ActivityHelper.AspNetListenerName)
                {
                    listener.Subscribe(new TestDiagnosticListener(onNext),
                        (name, arg1, arg2) => name == ActivityName);
                }
            });
        }

        private void EnableAspNetListenerOnly(Action<KeyValuePair<string, object>> onNext = null)
        {
            DiagnosticListener.AllListeners.Subscribe(listener =>
            {
                // if AspNetListener has subscription, then it is enabled
                if (listener.Name == ActivityHelper.AspNetListenerName)
                {
                    listener.Subscribe(new TestDiagnosticListener(onNext), 
                        activityName => false);
                }
            });
        }
        #endregion

        #region Helper Class        
        private class TestDiagnosticListener : IObserver<KeyValuePair<string, object>>
        {
            Action<KeyValuePair<string, object>> _onNextCallBack;

            public TestDiagnosticListener(Action<KeyValuePair<string, object>> onNext)
            {
                _onNextCallBack = onNext;
            }

            public void OnCompleted()
            {                
            }

            public void OnError(Exception error)
            {
            }

            public void OnNext(KeyValuePair<string, object> value)
            {
                _onNextCallBack?.Invoke(value);
            }
        }

        private class TestHttpRequest : HttpRequestBase
        {
            NameValueCollection _headers = new NameValueCollection();
            public override NameValueCollection Headers
            {
                get
                {
                    return _headers;
                }
            }

            public override UnvalidatedRequestValuesBase Unvalidated => new TestUnvalidatedRequestValues(_headers);
        }

        private class TestUnvalidatedRequestValues : UnvalidatedRequestValuesBase
        {
            NameValueCollection _headers = new NameValueCollection();

            public TestUnvalidatedRequestValues(NameValueCollection headers)
            {
                this._headers = headers;
            }

            public override NameValueCollection Headers => _headers;
        }

        private class TestHttpResponse : HttpResponseBase
        {
        }

        private class TestHttpServerUtility : HttpServerUtilityBase
        {
            HttpContextBase _context;

            public TestHttpServerUtility(HttpContextBase context)
            {
                _context = context;
            }

            public override Exception GetLastError()
            {
                return _context.Error;
            }
        }

        private class TestHttpContext : HttpContextBase
        {
            HttpRequestBase _request;
            HttpResponseBase _response;
            HttpServerUtilityBase _server;
            Hashtable _items;
            Exception _error;

            public TestHttpContext(Exception error = null)
            {
                _request = new TestHttpRequest();
                _response = new TestHttpResponse();
                _server = new TestHttpServerUtility(this);
                _items = new Hashtable();
                _error = error;
            }

            public override HttpRequestBase Request
            {
                get
                {
                    return _request;
                }
            }

            public override IDictionary Items
            {
                get
                {
                    return _items;
                }
            }

            public override Exception Error
            {
                get
                {
                    return _error;
                }
            }

            public override HttpServerUtilityBase Server
            {
                get
                {
                    return _server;
                }
            }
        }
        #endregion
    }

    static class PropertyExtensions
    {
        public static object GetProperty(this object _this, string propertyName)
        {
            return _this.GetType().GetTypeInfo().GetDeclaredProperty(propertyName)?.GetValue(_this);
        }
    }
}
