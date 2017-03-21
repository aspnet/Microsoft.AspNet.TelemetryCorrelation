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

namespace Microsoft.AspNet.CorrelationActivity.Tests
{
    public class ActivityHelperTest
    {
        private const string TestActivityName = "Activity.Test";
        private List<KeyValuePair<string, string>> _baggageItems = new List<KeyValuePair<string, string>>();
        private string _baggageInHeader;

        public ActivityHelperTest()
        {
            _baggageItems.Add(new KeyValuePair<string, string>("TestKey1", "123"));
            _baggageItems.Add(new KeyValuePair<string, string>("TestKey2", "456"));
            _baggageItems.Add(new KeyValuePair<string, string>("TestKey1", "789"));

            _baggageInHeader = "TestKey1=123,TestKey2=456,TestKey1=789";
            // reset static fields
            var allListenerField = typeof(DiagnosticListener).
                GetField("s_allListenerObservable", BindingFlags.Static | BindingFlags.NonPublic);
            allListenerField.SetValue(null, null);
            var aspnetListenerField = typeof(ActivityHelper).
                GetField("s_aspNetListener", BindingFlags.Static | BindingFlags.NonPublic);
            aspnetListenerField.SetValue(null, new DiagnosticListener(ActivityHelper.AspNetListenerName));
        }                

        #region RestoreCurrentActivity tests
        [Fact]
        public void Should_Not_Restore_If_ActivityCurrent_Is_Available()
        {
            var rootActivity = CreateActivity();
            var context = CreateHttpContext();
            rootActivity.Start();

            var restoredActivity = ActivityHelper.RestoreCurrentActivity(context);
            Assert.Null(restoredActivity);
        }

        [Fact]
        public void Should_Not_Restore_If_Root_Activity_Is_Not_In_HttpContext()
        {
            var context = CreateHttpContext();
            var restoredActivity = ActivityHelper.RestoreCurrentActivity(context);
            Assert.Null(restoredActivity);
        }

        [Fact]
        public void Can_Restore_Activity()
        {
            var rootActivity = CreateActivity();
            rootActivity.Start();
            var context = CreateHttpContext();
            context.Items[ActivityHelper.ActivityKey] = rootActivity;

            ExecutionContext.SuppressFlow();
            Task.Run(() =>
            {
                var restoredActivity = ActivityHelper.RestoreCurrentActivity(context);

                Assert.NotNull(restoredActivity);
                Assert.True(rootActivity.Id == restoredActivity.ParentId);
                Assert.True(!string.IsNullOrEmpty(restoredActivity.Id));
                var expectedBaggage = _baggageItems.OrderBy(item => item.Value);
                var actualBaggage = rootActivity.Baggage.OrderBy(item => item.Value);
                Assert.Equal(expectedBaggage, actualBaggage);
            }).Wait();
        }
        #endregion

        #region StopAspNetActivity tests
        [Fact]
        public void Can_Stop_Activity_Without_AspNetListener_Enabled()
        {
            var rootActivity = CreateActivity();
            rootActivity.Start();
            Thread.Sleep(100);
            ActivityHelper.StopAspNetActivity(rootActivity);

            Assert.True(rootActivity.Duration != TimeSpan.Zero);
            Assert.Null(rootActivity.Parent);
        }

        [Fact]
        public void Can_Stop_Activity_With_AspNetListener_Enabled()
        {
            var rootActivity = CreateActivity();
            rootActivity.Start();
            Thread.Sleep(100);
            EnableAspNetListenerOnly();
            ActivityHelper.StopAspNetActivity(rootActivity);

            Assert.True(rootActivity.Duration != TimeSpan.Zero);
            Assert.Null(rootActivity.Parent);
        }
        #endregion

        #region CreateRootActivity tests
        [Fact]
        public void Should_Not_Create_RootActivity_If_AspNetListener_Not_Enabled()
        {
            var context = CreateHttpContext();
            var rootActivity = ActivityHelper.CreateRootActivity(context);

            Assert.Null(rootActivity);
        }

        [Fact]
        public void Should_Not_Create_RootActivity_If_AspNetActivity_Not_Enabled()
        {
            var context = CreateHttpContext();
            EnableAspNetListenerOnly();
            var rootActivity = ActivityHelper.CreateRootActivity(context);

            Assert.Null(rootActivity);
        }

        [Fact]
        public void Can_Create_RootActivity_And_Restore_Info_From_Request_Header()
        {
            var requestHeaders = new NameValueCollection();
            requestHeaders.Add(ActivityExtensions.RequestIDHeaderName, "|aba2f1e978b2cab6.1");
            requestHeaders.Add(ActivityExtensions.CorrelationContextHeaderName, _baggageInHeader);

            var context = CreateHttpContext(requestHeaders);
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
            var context = CreateHttpContext();
            EnableAspNetListenerAndActivity();
            var rootActivity = ActivityHelper.CreateRootActivity(context);

            Assert.NotNull(rootActivity);
            Assert.True(!string.IsNullOrEmpty(rootActivity.Id));
        }

        [Fact]
        public void Can_Create_RootActivity_And_Saved_In_HttContext()
        {
            var context = CreateHttpContext();
            EnableAspNetListenerAndActivity();
            var rootActivity = ActivityHelper.CreateRootActivity(context);

            Assert.NotNull(rootActivity);
            Assert.Same(rootActivity, context.Items[ActivityHelper.ActivityKey]);
        }
        #endregion

        #region TriggerAspNetExceptionActivity tests
        [Fact]
        public void Should_Not_Trigger_AspNetExceptionActivity_If_AspNetExceptionActivity_Not_Enabled()
        {
            var activityTrigger = false;
            Action<KeyValuePair<string, object>> onNext = kvp => activityTrigger = true;
            EnableAspNetListenerAndActivity(onNext);
            var context = CreateHttpContext(null, new Exception("test"));

            ActivityHelper.WriteExceptionToDiagnosticSource(context);

            Assert.True(!activityTrigger);
        }

        [Fact]
        public void Can_Trigger_AspNetExceptionActivity_If_AspNetExceptionActivity_Enabled()
        {
            object loggedContext = null;
            Action<KeyValuePair<string, object>> onNext = kvp => loggedContext = kvp.Value.GetProperty("ActivityException");
            EnableAspNetListenerAndActivity(onNext, ActivityHelper.AspNetExceptionActivityName);
            var exception = new Exception("test");
            var context = CreateHttpContext(null, exception);

            ActivityHelper.WriteExceptionToDiagnosticSource(context);

            Assert.Same(exception, loggedContext);
        }
        #endregion

        #region Helper methods               
        private Activity CreateActivity()
        {
            var activity = new Activity(TestActivityName);
            _baggageItems.ForEach(kv => activity.AddBaggage(kv.Key, kv.Value));

            return activity;
        }

        private HttpContextBase CreateHttpContext(NameValueCollection requestHeaders = null, Exception error = null)
        {
            var context = new TestHttpContext(error);

            if (requestHeaders != null)
            {
                context.Request.Headers.Add(requestHeaders);
            }

            return context;
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
                if(_onNextCallBack != null)
                {
                    _onNextCallBack(value);
                }
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
