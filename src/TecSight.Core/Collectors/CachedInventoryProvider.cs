using TecSight.Core.Models;

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

    public CachedInventoryProvider(IHardwareInventoryProvider inner, TimeSpan? ttl = null)
    {
        _inner = inner;
        _ttl = ttl ?? TimeSpan.FromMinutes(1);
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
            return _cached;
        }
    }
}