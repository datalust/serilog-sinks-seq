# Serilog.Sinks.Seq [![Build status](https://ci.appveyor.com/api/projects/status/t7qdv68pej6inukl/branch/master?svg=true)](https://ci.appveyor.com/project/serilog/serilog-sinks-seq/branch/master)

A Serilog sink that writes events to the [Seq](https://getseq.net) structured log server.

[![Package Logo](http://serilog.net/images/serilog-sink-seq-nuget.png)](http://nuget.org/packages/serilog.sinks.seq)

## Getting started

To get started install the _Serilog.Sinks.Seq_ package from Visual Studio's _NuGet_ console:

```powershell
PM> Install-Package Serilog.Sinks.Seq
```

Point the logger to Seq:

```powershell
Log.Logger = new LoggerConfiguration()
    .WriteTo.Seq("http://localhost:5341")
    .CreateLogger();
```

And use the Serilog logging methods to associate named properties with log events:

```csharp
Log.Error("Failed to log on user {ContactId}", contactId);
```

Then query log event properties like `ContactId` from the browser:

![Query in Seq](http://getseq.net/img/search-by-property.png?extern)

The sink supports durable (disk-buffered) log shipping, and can take advantage of Seq's API keys to authenticate clients and dynamically attach properties to events at the server-side. Visit the [full documentation](http://docs.getseq.net/docs/using-serilog) for examples.

## Configuring with XML

To adjust the Seq server URL at deployment time, it's often convenient to configure it using XML `<appSettings>`, in the `App.config` or `Web.config` file.

Before Serilog can be configured in XML, it must be enabled using the `LoggerConfiguration`:

```csharp
Log.Logger = new LoggerConfiguration()
    .ReadFrom.AppSettings()
    .CreateLogger();
```

When XML is used for configuration, it's not necessary to include the `WriteTo.Seq()` method. It is important however that the _Serilog.Sinks.Seq.dll_ assembly is present alongside the app's binaries.

The two settings typically included are:

```xml
<add key="serilog:using:Seq" value="Serilog.Sinks.Seq" />
<add key="serilog:write-to:Seq.serverUrl" value="http://localhost:5341" />
<add key="serilog:write-to:Seq.apiKey" value="[optional API key here]" />
```

Serilog's XML configuration has several other capabilities that are described on the [Serilog wiki](https://github.com/serilog/serilog/wiki/AppSettings).
