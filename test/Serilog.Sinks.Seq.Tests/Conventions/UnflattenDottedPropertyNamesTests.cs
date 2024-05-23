using System.Collections.Generic;
using System.Linq;
using Serilog.Events;
using Serilog.Sinks.Seq.Conventions;
using Xunit;

namespace Serilog.Sinks.Seq.Tests.Conventions;

public class UnflattenDottedPropertyNamesTests
{
    [Fact]
    public void DottedToNestedWorks()
    {
        var someDotted = new Dictionary<string, LogEventPropertyValue>
        {
            ["dotnet.ilogger.category"] = new ScalarValue("Test.App"),
            ["environment.name"] = new ScalarValue("Production"),
            ["environment.region"] = new ScalarValue("us-west-2"),
            ["environment.beverage.name"] = new ScalarValue("coffee"),
            ["environment.domains"] = new StructureValue(
                [
                    new LogEventProperty("example.com", new ScalarValue(42)),
                    new LogEventProperty("datalust.co", new ScalarValue(43))
                ]),
            ["scope"] = new StructureValue([]),
            ["scope.name"] = new ScalarValue("Gerald"),
            ["vegetable"] = new ScalarValue("Potato"),
            ["Scope"] = new ScalarValue("Periscope"),
            [".gitattributes"] = new ScalarValue("Text")
        };

        var expected = new Dictionary<string, LogEventPropertyValue>
        {
            ["dotnet"] = new StructureValue(
            [
                new LogEventProperty("ilogger", new StructureValue(
                [
                    new LogEventProperty("category", new ScalarValue("Test App"))
                ]))
            ]),
            ["environment"] = new StructureValue(
            [
                new LogEventProperty("name", new ScalarValue("Production")),
                new LogEventProperty("region", new ScalarValue("us-west-2")),
                new LogEventProperty("beverage", new StructureValue(
                [
                    new LogEventProperty("name", new ScalarValue("coffee"))
                ])),
                new LogEventProperty("domains", new StructureValue(
                [
                    new LogEventProperty("example.com", new ScalarValue(42)),
                    new LogEventProperty("datalust.co", new ScalarValue(43))
                ]))
            ]),
            ["scope"] = new StructureValue([]),
            ["scope.name"] = new ScalarValue("Gerald"),
            ["vegetable"] = new ScalarValue("Potato"),
            ["Scope"] = new ScalarValue("Periscope"),
            [".gitattributes"] = new ScalarValue("Text")
        };

        var actual = new UnflattenDottedPropertyNames().ProcessDottedPropertyNames(someDotted);

        Assert.Equal(expected.Count, actual.Count);
        foreach (var expectedProperty in expected)
        {
            Assert.True(actual.TryGetValue(expectedProperty.Key, out var actualProperty));
            AssertEquivalentValue(expectedProperty.Value, expectedProperty.Value);
        }
    }

    [Theory]
    [InlineData("", false)]
    [InlineData(".", false)]
    [InlineData("..", false)]
    [InlineData(".a", false)]
    [InlineData("a.", false)]
    [InlineData("a..b", false)]
    [InlineData("a.b..c", false)]
    [InlineData("a.b.", false)]
    [InlineData("a. .b", false)]
    [InlineData("1.0", false)]
    [InlineData("1", false)]
    [InlineData("a", false)]
    [InlineData("abc", false)]
    [InlineData("a.b", true)]
    [InlineData("a1.bc._._xd.e_", true)]
    public void OnlyProcessesValidDottedNames(string key, bool isValid)
    {
        var actual = UnflattenDottedPropertyNames.IsDottedIdentifier(key);
        Assert.Equal(isValid, actual);
    }
    
    static void AssertEquivalentValue(LogEventPropertyValue expected, LogEventPropertyValue actual)
    {
        switch (expected, actual)
        {
            case (ScalarValue expectedScalar, ScalarValue actualScalar):
            {
                Assert.Equal(expectedScalar.Value, actualScalar.Value);
                break;
            }
            case (StructureValue expectedStructure, StructureValue actualStructure):
            {
                Assert.Equal(expectedStructure.TypeTag, actualStructure.TypeTag);
                Assert.Equal(expectedStructure.Properties.Count, actualStructure.Properties.Count);
                var actualProperties = actualStructure.Properties.ToDictionary(p => p.Name, p => p.Value);
                foreach (var expectedProperty in expectedStructure.Properties)
                {
                    var actualValue = Assert.Contains(expectedProperty.Name, actualProperties);
                    AssertEquivalentValue(expectedProperty.Value, actualValue);
                }
                break;
            }
            default:
            {
                Assert.Equal(expected, actual);
                break;
            }
        }
    }
}
