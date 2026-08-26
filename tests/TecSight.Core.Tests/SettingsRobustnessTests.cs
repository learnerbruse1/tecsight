using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using TecSight.App;
using TecSight.App.Localization;
using TecSight.App.Pages;

namespace TecSight.Core.Tests;

/// <summary>
/// 回归：设置窗口切换语言/保存后，导航选中态与各页面渲染必须保持稳定（曾出现切换语言后点击界面报异常）。
/// </summary>
public class SettingsRobustnessTests
{
    [Fact]
    public void NavSelectionAndTitles_SurviveLanguageSwitch()
    {
        RichFixtures.RunSta(() =>
        {
            var loc = LocalizationManager.Instance;
            var original = loc.CurrentLanguage;
            try
            {
                loc.CurrentLanguage = "zh";
                var vm = new MainViewModel(new HistoryCollector(new RichFixtures.Collector(), capacity: 10));
                var list = new ListBox
                {
                    SelectedValuePath = "Page",
                    DataContext = vm,
                };
                list.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("NavEntries"));
                list.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedValueProperty,
                    new Binding("CurrentPage") { Mode = BindingMode.TwoWay });

                Assert.Equal(AppPage.Overview, list.SelectedValue);
                Assert.Equal("概览", ((NavEntry)vm.NavEntries[0]).Title);

                loc.CurrentLanguage = "en";

                Assert.Equal(AppPage.Overview, list.SelectedValue); // 选中态不丢失
                Assert.Equal("Overview", ((NavEntry)vm.NavEntries[0]).Title); // 标题原地更新
                Assert.Equal(12, vm.NavEntries.Count);
            }
            finally
            {
                loc.CurrentLanguage = original;
            }
        });
    }

    [Fact]
    public void AllPages_RenderAfterLanguageAndSettingsChanges()
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

                var overview = new OverviewPage();
                var detail = new DetailPage();
                var processes = new ProcessesPage();
                var peripherals = new PeripheralsPage();

                foreach (var page in Enum.GetValues<AppPage>())
                {
                    vm.CurrentPage = page;
                    Render(page, vm, overview, detail, processes, peripherals);
                }

                // 模拟 SettingsWindow 保存后的流程：语言切换 + 模型失效 + 噪音开关变更
                loc.CurrentLanguage = original == "zh" ? "en" : "zh";
                detail.InvalidateModel();
                detail.SetHideNetworkNoise(true);
                foreach (var page in Enum.GetValues<AppPage>())
                {
                    vm.CurrentPage = page;
                    Render(page, vm, overview, detail, processes, peripherals);
                }
            }
            finally
            {
                loc.CurrentLanguage = original;
            }
        });
    }

    private static void Render(
        AppPage page, MainViewModel vm, OverviewPage overview, DetailPage detail,
        ProcessesPage processes, PeripheralsPage peripherals)
    {
        switch (page)
        {
            case AppPage.Overview:
                overview.Update(vm);
                break;
            case AppPage.Processes:
                processes.Update(vm);
                break;
            case AppPage.Peripherals:
                peripherals.Update(vm);
                break;
            default:
                detail.SetCategory(page);
                detail.Update(vm);
                break;
        }
    }
}
