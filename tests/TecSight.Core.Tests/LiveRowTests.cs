using TecSight.App.Models;

namespace TecSight.Core.Tests;

public class LiveRowTests
{
    [Fact]
    public void Value_Change_RaisesPropertyChanged()
    {
        var row = new LiveRow("Label", "A");
        var changed = new List<string?>();
        row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        row.Value = "B";

        Assert.Contains(nameof(LiveRow.Value), changed);
        Assert.Equal("B", row.Value);
    }

    [Fact]
    public void Value_SameValue_DoesNotRaisePropertyChanged()
    {
        var row = new LiveRow("Label", "A");
        var changed = new List<string?>();
        row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        row.Value = "A";

        Assert.Empty(changed);
    }

    [Fact]
    public void Spark_Change_RaisesPropertyChanged()
    {
        var row = new LiveRow("Label");
        var first = new double?[] { 1, 2 };
        var second = new double?[] { 3, 4 };
        var changed = new List<string?>();
        row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        row.Spark = first;
        row.Spark = second;

        Assert.Contains(nameof(LiveRow.Spark), changed);
        Assert.Same(second, row.Spark);
    }
}
