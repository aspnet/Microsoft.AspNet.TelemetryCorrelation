# Telemetry correlation http module

Telemetry correlation http module enables cross tier telemetry tracking. 

- Reads http headers
- Start/Stops Activity for the http request
- Ensure the Activity ambient state is transferred thru the IIS callbacks

See http protocol [specifications](https://github.com/dotnet/corefx/blob/master/src/System.Diagnostics.DiagnosticSource/src/HttpCorrelationProtocol.md) for details.


This http module is used by Application Insights. See [documentation](https://docs.microsoft.com/azure/application-insights/application-insights-correlation) and [code](https://github.com/Microsoft/ApplicationInsights-dotnet-server).

