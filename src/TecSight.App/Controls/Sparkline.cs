using System.Windows;
using System.Windows.Media;

namespace TecSight.App.Controls;

/// <summary>
/// 轻量迷你曲线控件：把数值序列画成折线，自动归一化。
/// 渲染时按像素宽度下采样，避免超长历史（3600 点）导致绘制开销过大。
/// </summary>
public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IEnumerable<double?>), typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable<double?>? Values
    {
        get => (IEnumerable<double?>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(Sparkline),
        new FrameworkPropertyMetadata(Brushes.DodgerBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        // 详情页传入的是数组：走索引快路径，避免每秒每个指标多次复制 3600 点序列；
        // 其他 IEnumerable 仍走通用路径，保持兼容。
        if (Values is double?[] values)
        {
            RenderArray(dc, values);
            return;
        }
        RenderEnumerable(dc, Values);
    }

    private void RenderArray(DrawingContext dc, double?[] values)
    {
        if (values.Length < 2) return;

        var w = Math.Max(2, ActualWidth);
        var h = Math.Max(1, ActualHeight);

        // 下采样：只保留最近约每像素一个点
        var maxPoints = Math.Max(2, (int)Math.Ceiling(w));
        var start = Math.Max(0, values.Length - maxPoints);
        var count = values.Length - start;
        if (count < 2) return;

        var min = double.PositiveInfinity;
        var max = double.NegativeInfinity;
        for (var i = start; i < values.Length; i++)
        {
            var value = values[i];
            if (!value.HasValue || double.IsNaN(value.Value)) continue;
            var x = value.Value;
            if (x < min) min = x;
            if (x > max) max = x;
        }
        if (double.IsPositiveInfinity(min)) return;
        if (max - min < 1e-9) { max = min + 1; min -= 1; }

        var pen = new Pen(Stroke, 1.2);
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            var started = false;
            for (var i = start; i < values.Length; i++)
            {
                var value = values[i];
                if (!value.HasValue || double.IsNaN(value.Value)) continue;
                var x = (i - start) * (w / (count - 1));
                var y = h - (value.Value - min) / (max - min) * (h - 2) - 1;
                if (!started) { ctx.BeginFigure(new Point(x, y), false, false); started = true; }
                else ctx.LineTo(new Point(x, y), true, false);
            }
        }
        geo.Freeze();
        dc.DrawGeometry(null, pen, geo);
    }

    private void RenderEnumerable(DrawingContext dc, IEnumerable<double?>? values)
    {
        var all = values?.ToList() ?? [];
        if (all.Count < 2) return;

        var w = Math.Max(2, ActualWidth);
        var h = Math.Max(1, ActualHeight);

        // 下采样：只保留最近约每像素一个点
        var maxPoints = Math.Max(2, (int)Math.Ceiling(w));
        var start = Math.Max(0, all.Count - maxPoints);
        var pts = all.Skip(start).ToList();
        if (pts.Count < 2) return;

        var valid = pts.Where(p => p.HasValue).Select(p => p!.Value).ToList();
        if (valid.Count == 0) return;
        var min = valid.Min();
        var max = valid.Max();
        if (max - min < 1e-9) { max = min + 1; min -= 1; }

        var pen = new Pen(Stroke, 1.2);
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            var started = false;
            for (var i = 0; i < pts.Count; i++)
            {
                if (!pts[i].HasValue) continue;
                var x = i * (w / (pts.Count - 1));
                var y = h - (pts[i]!.Value - min) / (max - min) * (h - 2) - 1;
                if (!started) { ctx.BeginFigure(new Point(x, y), false, false); started = true; }
                else ctx.LineTo(new Point(x, y), true, false);
            }
        }
        geo.Freeze();
        dc.DrawGeometry(null, pen, geo);
    }
}
