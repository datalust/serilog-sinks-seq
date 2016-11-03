using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.Seq.Tests.Support;
using Xunit;

namespace Serilog.Sinks.Seq.Tests
{
    public class ControlledLevelSwitchTests
    {
        [Fact]
        public void WhenTheServerSendsALevelTheSwitchIsAdjusted()
        {
            var lls = new LoggingLevelSwitch(LogEventLevel.Warning);
            var cls = new ControlledLevelSwitch(lls);
            cls.Update(LogEventLevel.Debug);
            Assert.Equal(LogEventLevel.Debug, lls.MinimumLevel);
        }

        [Fact]
        public void WhenTheServerSendsNoLevelTheSwitchIsNotInitiallyAdjusted()
        {
            var lls = new LoggingLevelSwitch(LogEventLevel.Warning);
            lls.MinimumLevel = LogEventLevel.Fatal;
            var cls = new ControlledLevelSwitch(lls);
            cls.Update(null);
            Assert.Equal(LogEventLevel.Fatal, lls.MinimumLevel);
        }

        [Fact]
        public void WhenTheServerSendsNoLevelTheSwitchIsResetIfPreviouslyAdjusted()
        {
            var lls = new LoggingLevelSwitch(LogEventLevel.Warning);
            var cls = new ControlledLevelSwitch(lls);
            cls.Update(LogEventLevel.Information);
            cls.Update(null);
            Assert.Equal(LogEventLevel.Warning, lls.MinimumLevel);
        }

        [Fact]
        public void WithNoSwitchToControlAllEventsAreIncluded()
        {
            var cls = new ControlledLevelSwitch(null);
            Assert.True(cls.IsIncluded(Some.DebugEvent()));
        }

        [Fact]
        public void WithNoSwitchToControlEventsAreStillFiltered()
        {
            var cls = new ControlledLevelSwitch(null);
            cls.Update(LogEventLevel.Warning);
            Assert.True(cls.IsIncluded(Some.ErrorEvent()));
            Assert.False(cls.IsIncluded(Some.InformationEvent()));
        }

        [Fact]
        public void WithNoSwitchToControlAllEventsAreIncludedAfterReset()
        {
            var cls = new ControlledLevelSwitch(null);
            cls.Update(LogEventLevel.Warning);
            cls.Update(null);
            Assert.True(cls.IsIncluded(Some.DebugEvent()));
        }

        [Fact]
        public void WhenControllingASwitchTheControllerIsActive()
        {
            var cls = new ControlledLevelSwitch(new LoggingLevelSwitch());
            Assert.True(cls.IsActive);
        }

        [Fact]
        public void WhenNotControllingASwitchTheControllerIsNotActive()
        {
            var cls = new ControlledLevelSwitch();
            Assert.False(cls.IsActive);
        }

        [Fact]
        public void AfterServerControlhTheControllerIsAlwaysActive()
        {
            var cls = new ControlledLevelSwitch();

            cls.Update(LogEventLevel.Information);
            Assert.True(cls.IsActive);

            cls.Update(null);
            Assert.True(cls.IsActive);
        }
    }
}
