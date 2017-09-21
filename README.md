# Serilog.Sinks.Seq [![Build status](https://ci.appveyor.com/api/projects/status/t7qdv68pej6inukl/branch/master?svg=true)](https://ci.appveyor.com/project/serilog/serilog-sinks-seq/branch/master) [![NuGet](https://img.shields.io/nuget/v/Serilog.Sinks.Seq.svg)](https://nuget.org/packages/serilog.sinks.seq) [![Join the chat at https://gitter.im/serilog/serilog](https://img.shields.io/gitter/room/serilog/serilog.svg)](https://gitter.im/serilog/serilog)

A Serilog sink that writes events to the [Seq](https://getseq.net) structured log server. Supports .NET 4.5+, .NET Core, and platforms compatible with the [.NET Platform Standard](https://github.com/dotnet/corefx/blob/master/Documentation/architecture/net-platform-standard.md) 1.1 including Windows 8 & UWP, Windows Phone and Xamarin.

[![Package Logo](http://serilog.net/images/serilog-sink-seq-nuget.png)](http://nuget.org/packages/serilog.sinks.seq)

### Getting started

Install the _Serilog.Sinks.Seq_ package from Visual Studio's _NuGet_ console:

```powershell
PM> Install-Package Serilog.Sinks.Seq
```

Point the logger to Seq:

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.Seq("http://localhost:5341")
    .CreateLogger();
```

And use the Serilog logging methods to associate named properties with log events:

```csharp
Log.Error("Failed to log on user {ContactId}", contactId);
```

Then query log event properties like `ContactId` from the browser:

![Query in Seq](https://nblumhardt.github.io/images/seq-sink-screenshot.png)

When the application shuts down, [ensure any buffered events are propertly flushed to Seq](http://blog.merbla.com/2016/07/06/serilog-log-closeandflush/) by disposing the logger or calling `Log.CloseAndFlush()`:

```csharp
Log.CloseAndFlush();
```

The sink can take advantage of Seq's [API keys](http://docs.getseq.net/docs/api-keys) to authenticate clients and dynamically attach properties to events at the server-side. To use an API key, specify it in the `apiKey` parameter of `WriteTo.Seq()`.

### Configuring with XML

To adjust the Seq server URL at deployment time, it's often convenient to configure it using XML `<appSettings>`, in the `App.config` or `Web.config` file.

Before Serilog can be configured using XML, the [Serilog.Settings.AppSettings](https://nuget.org/packages/serilog.settings.appsettings) package must be installed and enabled using the `LoggerConfiguration`:

```csharp
Log.Logger = new LoggerConfiguration()
    .ReadFrom.AppSettings()
    .CreateLogger();
```

When XML is used for configuration, it's not necessary to include the `WriteTo.Seq()` method. It is important however that the _Serilog.Sinks.Seq.dll_ assembly is present alongside the app's binaries.

The settings typically included are:

```xml
<configuration>
  <appSettings>
    <add key="serilog:using:Seq" value="Serilog.Sinks.Seq" />
    <add key="serilog:write-to:Seq.serverUrl" value="http://localhost:5341" />
    <add key="serilog:write-to:Seq.apiKey" value="[optional API key here]" />
```

Serilog's XML configuration has several other capabilities that are described on the [Serilog wiki](https://github.com/serilog/serilog/wiki/AppSettings).

### Dynamic log level control

The Seq sink can dynamically adjust the logging level up or down based on the level associated with an API key in Seq. To use this feature, create a `LoggingLevelSwitch` to control the `MinimumLevel`, and pass this in the `controlLevelSwitch` parameter of `WriteTo.Seq()`:

```csharp
var levelSwitch = new LoggingLevelSwitch();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.ControlledBy(levelSwitch)
    .WriteTo.Seq("http://localhost:5341",
                 apiKey: "yeEZyL3SMcxEKUijBjN",
                 controlLevelSwitch: levelSwitch)
    .CreateLogger();
```

The equivalent configuration in XML would be

```xml
<configuration>
  <appSettings>
    <!-- declare the switch -->
    <add key="serilog:level-switch:$controlSwitch" value="Information" />
    <!-- use it to control the root logger -->
    <add key="serilog:minimum-level:controlled-by" value="$controlSwitch" />
    <add key="serilog:using:Seq" value="Serilog.Sinks.Seq" />
    <add key="serilog:write-to:Seq.serverUrl" value="http://localhost:5341" />
    <add key="serilog:write-to:Seq.apiKey" value="yeEZyL3SMcxEKUijBjN" />
    <!-- give the sink access to the switch -->
    <add key="serilog:write-to:Seq.controlLevelSwitch" value="$controlSwitch" />
```

For further information see the [Seq documentation](http://docs.getseq.net/docs/using-serilog#dynamic-level-control).

### Compact event format

Seq 3.3 accepts Serilog's more efficient [compact JSON format](https://github.com/serilog/serilog-formatting-compact/). To use this, configure the sink with `compact: true`:

```csharp
    .WriteTo.Seq("http://localhost:5341", compact: true)
```

### Troubleshooting

> Nothing showed up, what can I do?

If events don't appear in Seq after pressing the refresh button in the _filter bar_, either your application was unable to contact the Seq server, or else the Seq server rejected the log events for some reason.

#### Server-side issues

The Seq server may reject incoming events if they're missing a required API key, if the payload is corrupted somehow, or if the log events are too large to accept.

Server-side issues are diagnosed using the Seq _Ingestion Log_, which shows the details of any problems detected on the server side. The ingestion log is linked from the _Settings_ > _Diagnostics_ page in the Seq user interface.

#### Client-side issues

If there's no information in the ingestion log, the application was probably unable to reach the server because of network configuration or connectivity issues. These are reported to the application through Serilog's `SelfLog`.

Add the following line after the logger is configured to print any error information to the console:

```csharp
Serilog.Debugging.SelfLog.Enable(Console.Error);
```

If the console is not available, you can pass a delegate into `SelfLog.Enable()` that will be called with each error message:

```csharp
Serilog.Debugging.SelfLog.Enable(message => {
    // Do something with `message`
});
```

#### Troubleshooting checklist

 * Check the Seq _Ingestion Log_, as described in the _Server-side issues_ section above.
 * Turn on the Serilog `SelfLog` as described above to check for connectivity problems and other issues on the client side.
 * Make sure your application calls `Log.CloseAndFlush()`, or disposes the root `Logger`, before it exits - otherwise, buffered events may be lost.
 * If your app is a Windows console application, it is also important to close the console window by exiting the app; Windows console apps are terminated "hard" if the close button in the title bar is used, so events buffered for sending to Seq may be lost if you use it.
 * [Raise an issue](https://github.com/serilog/serilog-sinks-seq/issues), ask for help on the [Seq support forum](http://docs.getseq.net/discuss) or email **support@getseq.net**.
 
