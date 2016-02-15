using System.Linq;
using System.Threading;
using Serilog;
using Serilog.Core;

namespace Sample
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // By sharing 
            var levelSwitch = new LoggingLevelSwitch();

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(levelSwitch)
                .WriteTo.LiterateConsole()
                .WriteTo.Seq("http://localhost:5341",
                             apiKey: "yeEZyL3SMcxEKUijBjN",
                             controlLevelSwitch: levelSwitch)
                .CreateLogger();

            Log.Information("Sample starting up");

            foreach (var i in Enumerable.Range(0, 1000))
            {
                Log.Information("Running loop {Counter}", i);

                Thread.Sleep(1000);
                Log.Debug("Loop iteration done");
            }
        }
    }
}
