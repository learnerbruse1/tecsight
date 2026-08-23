using TecSight.Core.Models;
using System.Text.Json;

namespace TecSight.Core;

/// <summary>
/// 硬件清单缓存装饰器：静态清单按 TTL 缓存，避免每次采集都执行慢速 WMI 查询。
/// 默认 60 秒刷新一次。
/// </summary>
public sealed class CachedInventoryProvider : IHardwareInventoryProvider
{
    private readonly IHardwareInventoryProvider _inner;
    private readonly TimeSpan _ttl;
    private readonly object _gate = new();
    private HardwareInventory? _cached;
    private DateTimeOffset _lastCaptured = DateTimeOffset.MinValue;
    private readonly string? _cacheFilePath;

    public CachedInventoryProvider(IHardwareInventoryProvider inner, TimeSpan? ttl = null, string? cacheFilePath = null)
    {
        _inner = inner;
        _ttl = ttl ?? TimeSpan.FromMinutes(1);
        _cacheFilePath = cacheFilePath;
        _cached = LoadPersistent();
        if (_cached is not null)
        {
            _lastCaptured = DateTimeOffset.UtcNow;
        }
    }

    public string Name => "cached-" + _inner.Name;

    public HardwareInventory Capture()
    {
        lock (_gate)
        {
            if (_cached is not null && DateTimeOffset.UtcNow - _lastCaptured < _ttl)
            {
                return _cached;
            }
            _cached = _inner.Capture() ?? new HardwareInventory();
            _lastCaptured = DateTimeOffset.UtcNow;
            SavePersistent(_cached);
            return _cached;
        }
    }

    private HardwareInventory? LoadPersistent()
    {
        if (string.IsNullOrWhiteSpace(_cacheFilePath) || !File.Exists(_cacheFilePath)) return null;
        try
        {
            return JsonSerializer.Deserialize<HardwareInventory>(File.ReadAllText(_cacheFilePath));
        }
        catch
        {
            return null;
        }
    }

    private void SavePersistent(HardwareInventory inventory)
    {
        if (string.IsNullOrWhiteSpace(_cacheFilePath)) return;
        try
        {
            var dir = Path.GetDirectoryName(_cacheFilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_cacheFilePath, JsonSerializer.Serialize(inventory));
        }
        catch
        {
            // 缓存写入失败不影响主流程
        }
    }
}
