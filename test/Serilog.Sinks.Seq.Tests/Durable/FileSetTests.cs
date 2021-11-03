using System.IO;
using Serilog.Sinks.Seq.Durable;
using Serilog.Sinks.Seq.Tests.Support;
using Xunit;

namespace Serilog.Sinks.Seq.Tests.Durable
{
    public class FileSetTests
    {
        [Fact]
        public void MatchingBufferFilenamesAreFoundAndOrdered()
        {
            using var tmp = new TempFolder();
            var bbf = Path.GetFullPath(Path.Combine(tmp.Path, "buffer"));
            const string? fakeContent = "{}";

            // Matching
            var shouldMatch = new[] {bbf + "-20180101.json", bbf + "-20180102.json", bbf + "-20180102_001.json"};
            foreach (var fn in shouldMatch)
                System.IO.File.WriteAllText(fn, fakeContent);

            // Ignores bookmark
            System.IO.File.WriteAllText(bbf + ".bookmark", fakeContent);

            // Ignores file with name suffix
            System.IO.File.WriteAllText(bbf + "similar-20180101.json", fakeContent);

            // Ignores file from unrelated set
            System.IO.File.WriteAllText(Path.Combine(Path.GetDirectoryName(bbf)!, "unrelated-20180101.json"), fakeContent);

            var fileSet = new FileSet(bbf);
            var files = fileSet.GetBufferFiles();
                
            Assert.Equal(3, files.Length);
            Assert.Equal(shouldMatch, files);
        }
    }
}
