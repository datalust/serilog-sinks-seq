using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BlazorWasm;
using Serilog;

Log.Logger = new LoggerConfiguration()
    // To send logs to a Seq instance running on the origin domain, build a URL with builder.HostEnvironment.BaseAddress. It's
    // fine to specify a different Seq URL as we've done here though, too.
    .WriteTo.Seq("https://seq.example.com")
    .WriteTo.BrowserConsole()
    .CreateLogger();

// Serilog can be used directly in Blazor; structured logging and property capturing work as usual, but in the case
// of the ProcessArchitecture enum we need to add ToString() to prevent trimming from removing the friendly descriptions :-)
Log.Information("Hello from {Architecture} on {Platform}!", RuntimeInformation.ProcessArchitecture.ToString(), RuntimeInformation.OSDescription);

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Remove this line if you prefer the built-in logger's console output.
builder.Logging.ClearProviders();

// This routes events from the built-in ILogger<T> through Serilog - optional, if you use Serilog directly.
builder.Logging.AddSerilog();

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

await builder.Build().RunAsync();
