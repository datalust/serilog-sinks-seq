using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Serilog.Sinks.Seq.Tests.Support;

class TempFolder : IDisposable
{
    static readonly Guid Session = Guid.NewGuid();

    readonly string _tempFolder;

    public TempFolder([CallerMemberName] string? name = null)
    {
        _tempFolder = System.IO.Path.Combine(
            Environment.GetEnvironmentVariable("TMP") ?? Environment.GetEnvironmentVariable("TMPDIR") ?? "/tmp",
            "Serilog.Sinks.Seq.Tests",
            Session.ToString("n"),
            name ?? Guid.NewGuid().ToString("n"));

        Directory.CreateDirectory(_tempFolder);
    }

    public string Path => _tempFolder;

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempFolder))
                Directory.Delete(_tempFolder, true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    public string AllocateFilename(string? ext = null)
    {
        return System.IO.Path.Combine(Path, Guid.NewGuid().ToString("n") + "." + (ext ?? "tmp"));
    }
}