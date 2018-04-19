using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Xunit;

namespace Microsoft.AspNet.TelemetryCorrelation.Tests
{
    public class HttpModuleTests
    {
        [Fact]
        public void OnStepDoesNotRestoreActivityWhenItIsNotLost()
        {
            var root = new Activity("root").Start();

            var context = HttpContextHelper.GetFakeHttpContext();
            ActivityHelper.SaveCurrentActivity(context, root);

            var module = new TelemetryCorrelationHttpModule();

            bool stepIsCalled = false;
            module.OnExecuteRequestStep(new HttpContextWrapper(context), () => 
            {
                stepIsCalled = true;
                Assert.Equal(root, Activity.Current);
            });

            Assert.Equal(root, Activity.Current);
            Assert.True(stepIsCalled);
        }

        [Fact]
        public void OnStepDoesNotRestoreActivityWhenThereIsNoActivityInContext()
        {
            bool stepIsCalled = false;
            var module = new TelemetryCorrelationHttpModule();
            module.OnExecuteRequestStep(HttpContextHelper.GetFakeHttpContextBase(), () =>
            {
                stepIsCalled = true;
                Assert.Null(Activity.Current);
            });

            Assert.Null(Activity.Current);
            Assert.True(stepIsCalled);
        }

        [Fact]
        public async Task OnStepRestoresActivity()
        {
            var context = HttpContextHelper.GetFakeHttpContext();

            DateTime start = DateTime.Now.AddSeconds(-1);
            var root = new Activity("root");
            root.AddBaggage("k", "v");
            root.SetStartTime(start);

            await Task.Run(() =>
            {
                root.Start();

                ActivityHelper.SaveCurrentActivity(context, root);
            });

            bool stepIsCalled = false;
            var module = new TelemetryCorrelationHttpModule();
            module.OnExecuteRequestStep(new HttpContextWrapper(context), () =>
            {
                stepIsCalled = true;
                AssertIsRestoredActivity(root, Activity.Current);
            });

            AssertIsRestoredActivity(root, Activity.Current);
            Assert.True(stepIsCalled);
        }

        private void AssertIsRestoredActivity(Activity original, Activity restored)
        {
            Assert.NotNull(restored);
            Assert.Equal(original.RootId, restored.RootId);
            Assert.Equal(original.Id, restored.ParentId);
            Assert.Equal(original.StartTimeUtc, restored.StartTimeUtc);
            Assert.False(string.IsNullOrEmpty(restored.Id));
            var expectedBaggage = original.Baggage.OrderBy(item => item.Value);
            var actualBaggage = restored.Baggage.OrderBy(item => item.Value);
            Assert.Equal(expectedBaggage, actualBaggage);
        }
    }
}
