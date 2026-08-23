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
    private TimeSpan _ttl;
    private readonly object _gate = new();
    private HardwareInventory? _cached;
    private string? _cachedJson;
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

    /// <summary>运行时更新缓存 TTL；下次采集时会立即采用新间隔。</summary>
    public void SetTtl(TimeSpan ttl)
    {
        if (ttl <= TimeSpan.Zero) return;
        lock (_gate)
        {
            _ttl = ttl;
        }
    }

    public HardwareInventory Capture()
    {
        lock (_gate)
        {
            if (_cached is not null && DateTimeOffset.UtcNow - _lastCaptured < _ttl)
            {
                return _cached;
            }

            try
            {
                var fresh = _inner.Capture();
                if (fresh is not null)
                {
                    var json = JsonSerializer.Serialize(fresh);
                    var changed = _cachedJson is null || !string.Equals(_cachedJson, json, StringComparison.Ordinal);
                    _cached = fresh;
                    _cachedJson = json;
                    _lastCaptured = DateTimeOffset.UtcNow;
                    if (changed) SavePersistent(json);
                    return fresh;
                }

                // 内部源返回 null 时优先保留最后一次可用缓存，避免界面突然变空。
                if (_cached is not null) return _cached;

                _cached = new HardwareInventory();
                _cachedJson = JsonSerializer.Serialize(_cached);
                _lastCaptured = DateTimeOffset.UtcNow;
                SavePersistent(_cachedJson);
                return _cached;
            }
            catch
            {
                // 内部源刷新失败时降级到旧缓存；无旧缓存则向上抛出，由采集器兜底为空清单。
                if (_cached is not null) return _cached;
                throw;
            }
        }
    }

    private HardwareInventory? LoadPersistent()
    {
        if (string.IsNullOrWhiteSpace(_cacheFilePath)) return null;
        try
        {
            var tmpPath = _cacheFilePath + ".tmp";
            try
            {
                if (!File.Exists(_cacheFilePath) && File.Exists(tmpPath))
                {
                    File.Move(tmpPath, _cacheFilePath);
                }
                else if (File.Exists(_cacheFilePath) && File.Exists(tmpPath))
                {
                    File.Delete(tmpPath);
                }
            }
            catch
            {
                // 临时文件清理失败不应阻止读取已存在的主缓存文件
            }
            if (!File.Exists(_cacheFilePath)) return null;
            var inventory = JsonSerializer.Deserialize<HardwareInventory>(File.ReadAllText(_cacheFilePath));
            if (inventory is not null)
            {
                Normalize(inventory);
                _cachedJson = JsonSerializer.Serialize(inventory);
            }
            return inventory;
        }
        catch
        {
            return null;
        }
    }

    private static void Normalize(HardwareInventory inventory)
    {
        inventory.Cpus = (inventory.Cpus ?? []).Where(x => x is not null).ToList();
        inventory.MemoryModules = (inventory.MemoryModules ?? []).Where(x => x is not null).ToList();
        inventory.Disks = (inventory.Disks ?? []).Where(x => x is not null).ToList();
        inventory.Gpus = (inventory.Gpus ?? []).Where(x => x is not null).ToList();
        inventory.NetworkAdapters = (inventory.NetworkAdapters ?? []).Where(x => x is not null).ToList();
        inventory.NetworkConfigurations = (inventory.NetworkConfigurations ?? []).Where(x => x is not null).ToList();
        inventory.LogicalDisks = (inventory.LogicalDisks ?? []).Where(x => x is not null).ToList();
        inventory.WifiInterfaces = (inventory.WifiInterfaces ?? []).Where(x => x is not null).ToList();
        inventory.ProblemDevices = (inventory.ProblemDevices ?? []).Where(x => x is not null).ToList();
        inventory.Displays = (inventory.Displays ?? []).Where(x => x is not null).ToList();
        inventory.AudioDevices = (inventory.AudioDevices ?? []).Where(x => x is not null).ToList();
        inventory.UsbDevices = (inventory.UsbDevices ?? []).Where(x => x is not null).ToList();
        inventory.Keyboards = (inventory.Keyboards ?? []).Where(x => x is not null).ToList();
        inventory.PointingDevices = (inventory.PointingDevices ?? []).Where(x => x is not null).ToList();
        inventory.Printers = (inventory.Printers ?? []).Where(x => x is not null).ToList();
    }

    private void SavePersistent(string json)
    {
        if (string.IsNullOrWhiteSpace(_cacheFilePath)) return;
        try
        {
            var dir = Path.GetDirectoryName(_cacheFilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var tmp = _cacheFilePath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, _cacheFilePath, overwrite: true);
        }
        catch
        {
            // 缓存写入失败不影响主流程
        }
    }
}
