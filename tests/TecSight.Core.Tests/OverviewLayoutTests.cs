using TecSight.App;
using TecSight.App.Localization;
using TecSight.App.Models;
using TecSight.App.Pages;

namespace TecSight.Core.Tests;

public class OverviewLayoutTests
{
    [Fact]
    public void OverviewCards_FollowSpecifiedLayoutWithoutMotherboard()
    {
        RichFixtures.RunSta(() =>
        {
            var loc = LocalizationManager.Instance;
            var original = loc.CurrentLanguage;
            try
            {
                loc.CurrentLanguage = "zh";
                var vm = new MainViewModel(new HistoryCollector(new RichFixtures.Collector(), capacity: 10));
                vm.SetSnapshot(vm.Collector.Collect());
                var page = new OverviewPage();
                page.Update(vm);

                var cards = ((IEnumerable<OverviewCard>)page.Cards.ItemsSource).ToList();
                Assert.Equal(12, cards.Count);
                Assert.Equal(
                    new[]
                    {
                        "CPU 使用率", "内存使用率", "磁盘 I/O", "GPU 使用率", "显存使用率", "网络",
                        "CPU 温度", "GPU 温度", "风扇转速", "运行时长", "电池", "系统",
                    },
                    cards.Select(c => c.Label));
                Assert.DoesNotContain(cards, c => c.Label == "主板");

                var vram = cards[4];
                Assert.Equal("显存使用率", vram.Label);
                Assert.StartsWith("24.4%", vram.Value);
                Assert.Contains("1.46 GB /", vram.Subtitle);

                Assert.Equal("网络", cards[5].Label);
                Assert.Equal("电池", cards[10].Label);
            }
            finally
            {
                loc.CurrentLanguage = original;
            }
        });
    }
}
