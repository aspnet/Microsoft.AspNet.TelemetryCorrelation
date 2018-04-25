using System;
using System.Collections.Generic;

namespace Microsoft.AspNet.TelemetryCorrelation.Tests
{
    class TestDiagnosticListener : IObserver<KeyValuePair<string, object>>
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
}