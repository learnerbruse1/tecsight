using System.ComponentModel;

namespace TecSight.App.Models;

/// <summary>详情页中的一个区块。</summary>
public sealed record DetailSection(string Title, IReadOnlyList<IDetailRow> Rows);

/// <summary>详情页中的一行：标签 + 值 + 可选迷你曲线。</summary>
public interface IDetailRow
{
    string Label { get; }
    string Value { get; }
    IReadOnlyList<double?>? Spark { get; }
}

/// <summary>静态行：内容固定，不随刷新变化。</summary>
public sealed record StaticRow(string Label, string Value) : IDetailRow
{
    public IReadOnlyList<double?>? Spark => null;
}

/// <summary>
/// 实时行：值/曲线随每次刷新变化，通过 INotifyPropertyChanged 原地更新，
/// 避免每秒钟重建控件树导致的滚动卡顿。
/// </summary>
public sealed class LiveRow : IDetailRow, INotifyPropertyChanged
{
    private string _value;
    private IReadOnlyList<double?>? _spark;

    public LiveRow(string label, string value = "", IReadOnlyList<double?>? spark = null)
    {
        Label = label;
        _value = value;
        _spark = spark;
    }

    public string Label { get; }

    public string Value
    {
        get => _value;
        set
        {
            if (_value == value) return;
            _value = value;
            OnPropertyChanged(nameof(Value));
        }
    }

    public IReadOnlyList<double?>? Spark
    {
        get => _spark;
        set
        {
            if (ReferenceEquals(_spark, value)) return;
            _spark = value;
            OnPropertyChanged(nameof(Spark));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>概览卡片。</summary>
public sealed record OverviewCard(string Label, string Value);