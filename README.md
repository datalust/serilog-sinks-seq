# Serilog.Sink.Seq [![Build status](https://ci.appveyor.com/api/projects/status/t7qdv68pej6inukl/branch/master?svg=true)](https://ci.appveyor.com/project/serilog/serilog-sinks-seq/branch/master)

![Package Logo](http://serilog.net/images/serilog-sink-seq-nuget.png)

A Serilog sink that writes events to the [Seq](https://getseq.net) structured log server.

## Getting started

To get started install the _Serilog.Sinks.Seq_ package from Visual Studio's _NuGet_ console:

```powershell
PM> Install-Package Serilog.Sinks.Seq
```

Point the logger to Seq:

```powershell
var log = new LoggerConfiguration()
    .WriteTo.Seq("http://localhost:5341")
    .CreateLogger();
```

And use the Serilog message template DSL to associate named properties with log events:

```csharp
log.Error("Failed to log on user {ContactId}", contactId);
```

Then query log event properties like `ContactId` from the browser:

![Query in Seq](http://getseq.net/img/search-by-property.png?extern)

