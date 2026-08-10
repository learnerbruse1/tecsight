using System.ComponentModel;
using System.Globalization;

namespace TecSight.App.Localization;

/// <summary>
/// 中英双语本地化管理器（本地化）。默认跟随系统语言，可在运行时切换。
/// 通过索引器绑定：{Binding Loc[Key]}；语言切换时触发 Item[] 变更以刷新绑定。
/// </summary>
public sealed class LocalizationManager : INotifyPropertyChanged
{
    public static LocalizationManager Instance { get; } = new();

    private readonly Dictionary<string, string> _zh = new();
    private readonly Dictionary<string, string> _en = new();
    private string _language;

    private LocalizationManager()
    {
        _language = DetectSystemLanguage();

        void Z(string k, string v) => _zh[k] = v;
        void E(string k, string v) => _en[k] = v;
        void Both(string k, string zh, string en) { Z(k, zh); E(k, en); }

        Both("App.Title", "TecSight — 硬件体检", "TecSight — Hardware Inspector");
        Both("Nav.Overview", "概览", "Overview");
        Both("Nav.Cpu", "CPU", "CPU");
        Both("Nav.Memory", "内存", "Memory");
        Both("Nav.Disk", "磁盘", "Storage");
        Both("Nav.Gpu", "显卡", "GPU");
        Both("Nav.Motherboard", "主板与系统", "Motherboard & OS");
        Both("Nav.Network", "网络", "Network");
        Both("Nav.Battery", "电池", "Battery");
        Both("Nav.Sensors", "传感器", "Sensors");
        Both("Nav.Processes", "进程", "Processes");
        Both("Nav.LangToggle", "EN / 中文", "中文 / EN");

        Both("Common.NotAvailable", "不可用", "N/A");
        Both("Common.Unknown", "未知", "Unknown");
        Both("Common.Good", "良好", "Good");
        Both("Common.Warning", "注意", "Warning");
        Both("Common.Critical", "危险", "Critical");

        Both("Export.Json", "导出 JSON", "Export JSON");
        Both("Export.Txt", "导出 TXT", "Export TXT");
        Both("Export.Compat", "兼容性报告", "Compat Report");
        Both("Export.Saved", "已导出：", "Exported: ");

        Both("Overview.Computer", "计算机", "Computer");
        Both("Overview.Cpu", "CPU 使用率", "CPU Usage");
        Both("Overview.Memory", "内存使用率", "Memory Usage");
        Both("Overview.Disk", "磁盘 I/O", "Disk I/O");
        Both("Overview.Gpu", "GPU 使用率", "GPU Usage");
        Both("Overview.Network", "网络", "Network");
        Both("Overview.Battery", "电池", "Battery");
        Both("Overview.KeyTemp", "关键温度", "Key Temp");
        Both("Overview.Motherboard", "主板", "Motherboard");
        Both("Overview.System", "系统", "System");
        Both("Overview.Down", "↓", "↓");
        Both("Overview.Up", "↑", "↑");
        Both("Overview.Refreshing", "实时刷新中（1 秒）", "Live refresh (1s)");
        Both("Overview.BatteryHealth", "健康度", "Health");

        Both("Detail.Inventory", "硬件清单", "Hardware Inventory");
        Both("Detail.Live", "运行指标", "Live Metrics");
        Both("Detail.Sensors", "传感器读数", "Sensor Readings");
        Both("Detail.Smart", "SMART 属性", "SMART Attributes");
        Both("Detail.History", "历史趋势（最近 1 小时）", "History (last hour)");
        Both("Detail.Model", "型号", "Model");
        Both("Detail.Cores", "核心数", "Cores");
        Both("Detail.Threads", "线程数", "Threads");
        Both("Detail.BaseClock", "基础频率", "Base Clock");
        Both("Detail.Manufacturer", "制造商", "Manufacturer");
        Both("Detail.Capacity", "容量", "Capacity");
        Both("Detail.Speed", "频率", "Speed");
        Both("Detail.PartNumber", "部件号", "Part Number");
        Both("Detail.Serial", "序列号", "Serial Number");
        Both("Detail.Slot", "插槽", "Slot");
        Both("Detail.Type", "类型", "Type");
        Both("Detail.ConfigClock", "实际频率", "Configured Clock");
        Both("Detail.Voltage", "电压", "Voltage");
        Both("Detail.Health", "健康度", "Health");
        Both("Detail.Vram", "显存", "VRAM");
        Both("Detail.Driver", "驱动版本", "Driver");
        Both("Detail.Bios", "BIOS", "BIOS");
        Both("Detail.Product", "产品", "Product");
        Both("Detail.Mac", "MAC", "MAC");
        Both("Detail.Physical", "物理设备", "Physical");
        Both("Detail.Charge", "电量", "Charge");
        Both("Detail.Charging", "充电中", "Charging");
        Both("Detail.Computer", "计算机", "Computer");
        Both("Detail.Os", "操作系统", "Operating System");
        Both("Detail.Uptime", "运行时长", "Uptime");
        Both("Detail.DesignCapacity", "设计容量", "Design Capacity");
        Both("Detail.FullChargeCapacity", "满充容量", "Full Charge Capacity");
        Both("Detail.BatteryLoss", "损耗", "Wear");
        Both("Detail.CpuUsage", "CPU 使用率", "CPU Usage");
        Both("Detail.MemUsage", "内存使用率", "Memory Usage");
        Both("Detail.MemUsed", "已用 / 总计", "Used / Total");
        Both("Detail.DiskRead", "读取", "Read");
        Both("Detail.DiskWrite", "写入", "Write");
        Both("Detail.NetDown", "下载", "Download");
        Both("Detail.NetUp", "上传", "Upload");
                Both("Detail.GpuUsage", "GPU 使用率", "GPU Usage");
        Both("Detail.GpuFreq", "核心频率", "GPU Clock");
        Both("Detail.VramTotal", "总显存", "Total VRAM");
        Both("Detail.VramUsed", "显存已用", "VRAM Used");
        Both("Detail.VramFree", "显存空闲", "VRAM Free");
        Both("Detail.VramUsage", "显存占用", "VRAM Usage");
        Both("Detail.NoSensors", "无传感器数据", "No sensor data");
        Both("Detail.NoSmart", "无 SMART 属性（可能需要管理员权限）", "No SMART attributes (may require admin)");
        Both("Detail.BatteryLevel", "电量", "Charge Level");
        Both("Detail.Ip", "IP 地址", "IP Address");
        Both("Detail.Gateway", "网关", "Gateway");
        Both("Detail.Dns", "DNS", "DNS");
        Both("Detail.Yes", "是", "Yes");
        Both("Detail.No", "否", "No");

        Both("Process.Name", "进程", "Process");
        Both("Process.Cpu", "CPU 占用", "CPU");
        Both("Process.Memory", "内存占用", "Memory");
    }

    public string CurrentLanguage
    {
        get => _language;
        set
        {
            if (_language == value) return;
            _language = value == "zh" ? "zh" : "en";
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
        }
    }

    public string this[string key] =>
        (_language == "zh" ? _zh : _en).TryGetValue(key, out var v) ? v : key;

    public event PropertyChangedEventHandler? PropertyChanged;

    private static string DetectSystemLanguage()
    {
        try
        {
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh" ? "zh" : "en";
        }
        catch
        {
            return "en";
        }
    }
}