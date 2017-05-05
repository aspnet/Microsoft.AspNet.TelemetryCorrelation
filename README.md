# Telemetry correlation http module

Telemetry correlation http module enables cross tier telemetry tracking. 

- Reads http headers
- Start/Stops Activity for the http request
- Ensure the Activity ambient state is transferred thru the IIS callbacks

See http protocol [specifications](https://github.com/lmolkova/corefx/blob/80e8a8d767a71f413fd7b2f11b507cd395110e8d/src/System.Diagnostics.DiagnosticSource/src/FlatRequestId.md) for details.


This http module is used by Application Insights. See [documentation](https://docs.microsoft.com/en-us/azure/application-insights/application-insights-correlation) and [code](https://github.com/Microsoft/ApplicationInsights-dotnet-server).

