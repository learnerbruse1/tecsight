namespace TecSight.App.Models;

/// <summary>详情页中的一个区块。</summary>
public sealed record DetailSection(string Title, IReadOnlyList<DetailRow> Rows);

/// <summary>详情页中的一行：标签 + 值 + 可选迷你曲线。</summary>
public sealed record DetailRow(string Label, string Value, IReadOnlyList<double?>? Spark = null);
/// <summary>概览卡片。</summary>
public sealed record OverviewCard(string Label, string Value);