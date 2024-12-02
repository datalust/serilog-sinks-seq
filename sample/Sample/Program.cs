using Serilog;
using Serilog.Core;
using Serilog.Debugging;

SelfLog.Enable(Console.Error);

// By sharing between the Seq sink and logger itself,
// Seq API keys can be used to control the level of the whole logging pipeline.
var levelSwitch = new LoggingLevelSwitch();

try
{
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.ControlledBy(levelSwitch)
        .WriteTo.Console()
        .WriteTo.Seq("http://localhost:5341", controlLevelSwitch: levelSwitch)
        .CreateLogger();

    Log.Information("Sample starting up");

    foreach (var i in Enumerable.Range(0, 100))
    {
        Log.Information("Running loop {Counter}, switch is at {Level}", i, levelSwitch.MinimumLevel);

        Thread.Sleep(1000);
        Log.Debug("Loop iteration done");
    }
}
catch (Exception ex)
{
    Log.Error(ex, "Unhandled exception");
}
finally
{
    await Log.CloseAndFlushAsync();
}
