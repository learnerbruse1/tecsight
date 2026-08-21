namespace TecSight.Core.Models;

/// <summary>
/// 外围设备（外设）：接入即显示。Category 为类型键（storage/keyboard/mouse/…），由界面本地化。
/// </summary>
public sealed record PeripheralDevice(
    string? Name,
    string? Manufacturer,
    string? Description,
    string Category,
    string? PnpClass,
    string? Detail,
    string? Status = null,
    string? PnpDeviceId = null,
    string? DriverProvider = null,
    string? DriverVersion = null,
    string? DriverDate = null,
    string? Service = null,
    string? DeviceId = null,
    int? ConfigManagerErrorCode = null,
    string? HardwareId = null);
