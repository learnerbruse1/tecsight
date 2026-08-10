using System.Windows;
using System.Windows.Media;

namespace TecSight.App.Controls;

/// <summary>轻量迷你曲线控件：把数值序列画成折线，自动归一化。</summary>
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
        var pts = Values?.Select(v => v).ToList() ?? [];
        if (pts.Count < 2) return;

        var w = Math.Max(1, ActualWidth);
        var h = Math.Max(1, ActualHeight);
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