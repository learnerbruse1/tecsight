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
                Both("Nav.Other", "其他设备", "Other Devices");
        Both("Nav.Peripherals", "外设", "Peripherals");
        Both("Nav.LangToggle", "EN / 中文", "中文 / EN");

        Both("Common.NotAvailable", "不可用", "N/A");
        Both("Common.Unknown", "未知", "Unknown");
        Both("Common.Good", "良好", "Good");
        Both("Common.Warning", "注意", "Warning");
                Both("Common.Critical", "危险", "Critical");
                Both("Common.RestartAsAdmin", "以管理员权限重启", "Restart as admin");
        Both("Common.Theme", "切换深浅主题", "Toggle dark/light theme");

        Both("Export.Json", "导出 JSON", "Export JSON");
        Both("Export.Txt", "导出 TXT", "Export TXT");
                Both("Export.Compat", "兼容性报告", "Compat Report");
                Both("Export.Copy", "复制摘要", "Copy Summary");
                Both("Export.Html", "导出 HTML", "Export HTML");
        Both("Export.Menu", "导出 ▾", "Export ▾");
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
        Both("Overview.CollectFailed", "⚠ 实时数据采集异常：性能计数器不可用（部分数据可能为 N/A）", "⚠ Live data unavailable: performance counters missing (some values may be N/A)");
                Both("Overview.BatteryHealth", "健康度", "Health");
        Both("Overview.CpuTemp", "CPU 温度", "CPU Temp");
        Both("Overview.GpuTemp", "GPU 温度", "GPU Temp");
        Both("Overview.Fan", "风扇转速", "Fan Speed");
        Both("Overview.Uptime", "运行时长", "Uptime");

        Both("Detail.Inventory", "硬件清单", "Hardware Inventory");
        Both("Detail.Live", "运行指标", "Live Metrics");
        Both("Detail.Sensors", "传感器读数", "Sensor Readings");
        Both("Detail.Smart", "SMART 属性", "SMART Attributes");
        Both("Detail.History", "历史趋势（最近 1 小时）", "History (last hour)");
        Both("Detail.Model", "型号", "Model");
        Both("Detail.Cores", "核心数", "Cores");
        Both("Detail.Threads", "线程数", "Threads");
                Both("Detail.BaseClock", "基础频率", "Base Clock");
        Both("Detail.Architecture", "架构", "Architecture");
        Both("Detail.Socket", "插槽", "Socket");
        Both("Detail.L2Cache", "L2 缓存", "L2 Cache");
        Both("Detail.L3Cache", "L3 缓存", "L3 Cache");
        Both("Detail.CurrentClock", "当前频率", "Current Clock");
        Both("Detail.ProcessorId", "处理器 ID", "Processor ID");
        Both("Detail.MediaType", "介质类型", "Media Type");
        Both("Detail.BusType", "总线", "Bus");
        Both("Detail.Firmware", "固件版本", "Firmware");
        Both("Detail.NetSpeed", "速率", "Speed");
        Both("Detail.NetType", "类型", "Type");
                Both("Detail.CycleCount", "循环次数", "Cycle Count");
        Both("Detail.Chemistry", "化学类型", "Chemistry");
        Both("Detail.DesignVoltage", "设计电压", "Design Voltage");
        Both("Detail.CurrentVoltage", "当前电压", "Current Voltage");
        Both("Detail.Chem.LiP", "锂聚合物", "Li-Polymer");
        Both("Detail.Chem.LiI", "锂离子", "Li-Ion");
        Both("Detail.Chem.NiMH", "镍氢", "NiMH");
        Both("Detail.Chem.NiCd", "镍镉", "NiCd");
        Both("Detail.Chem.PbAc", "铅酸", "Lead-Acid");
        Both("Detail.SystemManufacturer", "系统制造商", "System Manufacturer");
        Both("Detail.SystemModel", "系统型号", "System Model");
        Both("Detail.BiosDate", "BIOS 日期", "BIOS Date");
        Both("Detail.Displays", "显示器", "Displays");
        Both("Detail.Audio", "音频设备", "Audio Devices");
                Both("Detail.Usb", "USB 设备", "USB Devices");
        Both("Detail.Keyboards", "键盘", "Keyboards");
        Both("Detail.Mice", "鼠标", "Mice");
        Both("Detail.Printers", "打印机", "Printers");
        Both("Detail.Default", "默认", "Default");
        Both("Detail.OsArch", "系统架构", "OS Architecture");
        Both("Detail.FirmwareType", "固件类型", "Firmware Type");
        Both("Detail.InstallDate", "安装日期", "Installed");
                Both("Detail.LastBoot", "上次启动", "Last Boot");
        Both("Peripheral.Refresh", "刷新", "Refresh");
        Both("Peripheral.UpdatedAt", "更新于", "Updated");
        Both("Peripheral.Count", "共", "Total");
        Both("Peripheral.storage", "存储设备", "Storage");
        Both("Peripheral.keyboard", "键盘", "Keyboards");
        Both("Peripheral.mouse", "鼠标", "Mice");
        Both("Peripheral.camera", "摄像头", "Cameras");
        Both("Peripheral.audio", "音频设备", "Audio");
        Both("Peripheral.display", "显示器", "Displays");
        Both("Peripheral.network", "网络设备", "Network");
        Both("Peripheral.bluetooth", "蓝牙设备", "Bluetooth");
        Both("Peripheral.printer", "打印机", "Printers");
        Both("Peripheral.cardreader", "读卡器", "Card Readers");
        Both("Peripheral.gamepad", "游戏手柄", "Gamepads");
        Both("Peripheral.phone", "手机/便携设备", "Phones");
        Both("Peripheral.hub", "USB 集线器", "USB Hubs");
        Both("Peripheral.input", "输入设备", "Input");
        Both("Peripheral.usb", "USB 设备", "USB Devices");
        Both("Peripheral.other", "其他设备", "Other");
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
        Both("Detail.AppVersion", "软件版本", "Software Version");
        Both("Detail.Os", "操作系统", "Operating System");
        Both("Detail.Uptime", "运行时长", "Uptime");
        Both("Detail.DesignCapacity", "设计容量", "Design Capacity");
        Both("Detail.FullChargeCapacity", "满充容量", "Full Charge Capacity");
        Both("Detail.BatteryLoss", "损耗", "Wear");
                Both("Detail.CpuUsage", "CPU 使用率", "CPU Usage");
        Both("Detail.CpuFreq", "实时频率", "Live Clock");
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
        Both("Detail.NoCpuTemp", "未检测到 CPU 温度", "No CPU temperature detected");
        Both("Detail.AdminHint", "部分传感器（CPU 温度 / 风扇转速）需要管理员权限或硬件支持，可用右上角按钮以管理员权限重启", "Some sensors (CPU temp / fan speed) need admin rights or hardware support — use the top-right button to restart as admin");
        Both("Detail.BatteryLevel", "电量", "Charge Level");
        Both("Detail.Ip", "IP 地址", "IP Address");
        Both("Detail.Gateway", "网关", "Gateway");
        Both("Detail.Dns", "DNS", "DNS");
        Both("Detail.Yes", "是", "Yes");
        Both("Detail.No", "否", "No");

        Both("Process.Name", "进程", "Process");
        Both("Process.Cpu", "CPU 占用", "CPU");
                Both("Process.Memory", "内存占用", "Memory");
        Both("Process.Total", "进程总数", "Total processes");
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