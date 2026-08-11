using TecSight.App.Localization;

namespace TecSight.Core.Tests;

public class LocalizationManagerTests
{
    [Fact]
    public void MissingKey_ReturnsKeyItself()
    {
        var loc = LocalizationManager.Instance;

        Assert.Equal("No.Such.Key", loc["No.Such.Key"]);
    }

    [Fact]
    public void LanguageSwitch_ChangesValuesAndRaisesEvent()
    {
        var loc = LocalizationManager.Instance;
        var original = loc.CurrentLanguage;
        try
        {
            loc.CurrentLanguage = "en"; // 先强制到 en，确保后续切换必然触发变更事件
            var changedTo = "";
            loc.PropertyChanged += (_, e) => { if (e.PropertyName == "CurrentLanguage") changedTo = loc.CurrentLanguage; };

            loc.CurrentLanguage = "zh";
            Assert.Equal("概览", loc["Nav.Overview"]);
            Assert.Equal("zh", changedTo);

            loc.CurrentLanguage = "en";
            Assert.Equal("Overview", loc["Nav.Overview"]);
            Assert.Equal("en", changedTo);
        }
        finally
        {
            loc.CurrentLanguage = original;
        }
    }

    [Fact]
    public void InvalidLanguage_CoercesToEnglish()
    {
        var loc = LocalizationManager.Instance;
        var original = loc.CurrentLanguage;
        try
        {
            loc.CurrentLanguage = "fr"; // 非 zh 一律按 en 处理
            Assert.Equal("en", loc.CurrentLanguage);
            Assert.Equal("Overview", loc["Nav.Overview"]);
        }
        finally
        {
            loc.CurrentLanguage = original;
        }
    }
}
