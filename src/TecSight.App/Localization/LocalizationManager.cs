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
        Both("Nav.Bios", "BIOS", "BIOS");
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
        Both("Common.AlreadyRunning", "TecSight 已在运行。", "TecSight is already running.");
        Both("Common.UnhandledError", "发生未处理的错误：", "An unhandled error occurred:");
                Both("Common.ErrorLogged", "详情已写入日志。", "Details have been written to the log.");
        Both("Common.Copied", "✅ 已复制到剪贴板", "✅ Copied to clipboard");
        Both("Common.Theme", "切换深浅主题", "Toggle dark/light theme");
        Both("Common.Settings", "设置", "Settings");
        Both("Settings.Title", "设置", "Settings");
        Both("Settings.RefreshInterval", "检测频率", "Refresh interval");
        Both("Settings.PeripheralInterval", "外设扫描间隔", "Peripheral scan interval");
        Both("Settings.InventoryInterval", "硬件清单刷新间隔", "Hardware inventory refresh interval");
        Both("Settings.Language", "语言", "Language");
        Both("Settings.DarkTheme", "深色主题", "Dark theme");
        Both("Settings.Save", "保存", "Save");
        Both("Settings.Cancel", "取消", "Cancel");
        Both("Settings.Description", "设置实时指标刷新和外设扫描频率。", "Configure live metric refresh and peripheral scan frequency.");

        Both("Export.Json", "导出 JSON", "Export JSON");
        Both("Export.Txt", "导出 TXT", "Export TXT");
                Both("Export.Compat", "兼容性报告", "Compat Report");
                Both("Export.Copy", "复制摘要", "Copy Summary");
                Both("Export.Html", "导出 HTML", "Export HTML");
                Both("Export.Menu", "导出 ▾", "Export ▾");
        Both("Export.CsvHistory", "导出历史 CSV", "Export History CSV");
        Both("Export.Saved", "已导出：", "Exported: ");
        Both("Export.FilterHtml", "HTML 文件 (*.html)|*.html", "HTML Files (*.html)|*.html");
        Both("Export.FilterCsv", "CSV 文件 (*.csv)|*.csv", "CSV Files (*.csv)|*.csv");
        Both("Export.FilterJson", "JSON 文件 (*.json)|*.json", "JSON Files (*.json)|*.json");
        Both("Export.FilterTxt", "文本文件 (*.txt)|*.txt", "Text Files (*.txt)|*.txt");

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
                        Both("Overview.Refreshing", "实时刷新中", "Live refresh");
        Both("Overview.UpdatedAt", "更新于", "Updated at");
                Both("Overview.CollectFailed", "⚠ 实时数据采集异常：性能计数器不可用（部分数据可能为 N/A）", "⚠ Live data unavailable: performance counters missing (some values may be N/A)");
        Both("Overview.InventoryFailed", "⚠ 硬件清单读取异常：WMI 不可用（部分硬件信息可能为 N/A）", "⚠ Hardware inventory unavailable: WMI missing (some info may be N/A)");
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
        Both("Peripheral.None", "未检测到外设", "No peripherals detected");
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
        Both("Detail.HideNetworkNoise", "隐藏网络过滤器噪音", "Hide network filter noise");
                Both("Detail.NoSmart", "无 SMART 属性（可能需要管理员权限）", "No SMART attributes (may require admin)");
        Both("Detail.NoCpuTemp", "未检测到 CPU 温度", "No CPU temperature detected");
        Both("Detail.AdminHint", "部分传感器（CPU 温度 / 风扇转速）需要管理员权限或硬件支持，可用右上角按钮以管理员权限重启", "Some sensors (CPU temp / fan speed) need admin rights or hardware support — use the top-right button to restart as admin");
        Both("Detail.BatteryLevel", "电量", "Charge Level");
        Both("Detail.Ip", "IP 地址", "IP Address");
        Both("Detail.Gateway", "网关", "Gateway");
        Both("Detail.Dns", "DNS", "DNS");
        Both("Detail.Yes", "是", "Yes");
        Both("Detail.No", "否", "No");

        Both("Detail.BiosVersion", "BIOS 版本", "BIOS Version");
        Both("Detail.SmbiosVersion", "SMBIOS 版本", "SMBIOS Version");
        Both("Detail.Description", "描述", "Description");
        Both("Detail.BuildNumber", "构建号", "Build Number");
        Both("Detail.IdentificationCode", "标识码", "Identification Code");
        Both("Detail.LanguageEdition", "语言版本", "Language Edition");
        Both("Detail.SystemBiosVersion", "系统 BIOS 版本", "System BIOS Version");
        Both("Detail.EcVersion", "EC 固件版本", "EC Firmware Version");
        Both("Detail.PrimaryBios", "主 BIOS", "Primary BIOS");
        Both("Detail.Status", "状态", "Status");
        Both("Detail.BiosReadOnly", "本页面以只读方式读取 BIOS/UEFI 信息，不执行任何写入或修改操作。", "BIOS/UEFI information is read-only; no writes or modifications are performed.");

        Both("Detail.Interfaces", "物理接口", "Physical Interfaces");
        Both("Detail.ConnectedDevices", "已连接设备", "Connected Devices");
        Both("Detail.ConnectionStatus", "连接状态", "Connection Status");
        Both("Detail.PnpDeviceId", "PNP ID", "PNP ID");
        Both("Detail.Dhcp", "DHCP", "DHCP");
        Both("Detail.NetStatus.0", "未连接", "Disconnected");
        Both("Detail.NetStatus.1", "连接中", "Connecting");
        Both("Detail.NetStatus.2", "已连接", "Connected");
        Both("Detail.NetStatus.3", "断开中", "Disconnecting");
        Both("Detail.NetStatus.4", "硬件不存在", "Hardware not present");
        Both("Detail.NetStatus.5", "硬件已禁用", "Hardware disabled");
        Both("Detail.NetStatus.6", "硬件故障", "Hardware malfunction");
        Both("Detail.NetStatus.7", "媒体已断开", "Media disconnected");
        Both("Detail.NetStatus.8", "认证中", "Authenticating");
        Both("Detail.NetStatus.9", "认证成功", "Authentication succeeded");
        Both("Detail.NetStatus.10", "认证失败", "Authentication failed");
        Both("Detail.NetStatus.11", "地址无效", "Invalid address");
        Both("Detail.NetStatus.12", "需要凭据", "Credentials required");
        Both("Detail.NetStatus.Unknown", "未知", "Unknown");

        Both("Detail.PhysicalCpuCount", "物理 CPU 数量", "Physical CPUs");
        Both("Detail.Virtualization", "虚拟化 (VT-x/AMD-V)", "Virtualization (VT-x/AMD-V)");
        Both("Detail.Enabled", "已启用", "Enabled");
        Both("Detail.Disabled", "已禁用", "Disabled");

        Both("Detail.FormFactor", "外形", "Form Factor");
        Both("Detail.Ecc", "ECC", "ECC");
        Both("Detail.MemoryTopology", "内存拓扑", "Memory Topology");
        Both("Detail.TotalSlots", "插槽总数", "Total Slots");
        Both("Detail.UsedSlots", "已用插槽", "Used Slots");
        Both("Detail.MaxCapacity", "最大支持容量", "Max Capacity");
        Both("Detail.ErrorCorrection", "错误校正", "Error Correction");

        Both("Detail.Domain", "域 / 工作组", "Domain / Workgroup");
        Both("Detail.PartOfDomain", "已加入域", "Domain Joined");
        Both("Detail.TimeZone", "时区", "Time Zone");
        Both("Detail.SecureBoot", "安全启动", "Secure Boot");
        Both("Detail.Tpm", "TPM", "TPM");
        Both("Detail.Hypervisor", "Hypervisor", "Hypervisor");
        Both("Detail.SystemType", "系统类型", "System Type");

        Both("Detail.DriverDate", "驱动日期", "Driver Date");
        Both("Detail.Resolution", "分辨率", "Resolution");
        Both("Detail.RefreshRate", "刷新率", "Refresh Rate");
        Both("Detail.VideoMode", "视频模式", "Video Mode");
        Both("Detail.VideoProcessor", "视频处理器", "Video Processor");
        Both("Detail.VideoArchitecture", "视频架构", "Video Architecture");
        Both("Detail.AdapterCompatibility", "兼容性", "Compatibility");

        Both("Detail.LogicalDisks", "存储卷 / 分区", "Storage Volumes");
        Both("Detail.FileSystem", "文件系统", "File System");
        Both("Detail.TotalSpace", "总容量", "Total");
        Both("Detail.FreeSpace", "可用", "Free");
        Both("Detail.DriveType.Removable", "可移动", "Removable");
        Both("Detail.DriveType.Fixed", "本地磁盘", "Fixed");
        Both("Detail.DriveType.Network", "网络", "Network");
        Both("Detail.DriveType.Optical", "光盘", "Optical");
        Both("Detail.DriveType.Ram", "内存盘", "RAM Disk");
        Both("Detail.DriveType.Unknown", "未知", "Unknown");

        Both("Detail.Wifi", "Wi-Fi", "Wi-Fi");
        Both("Detail.WifiState", "状态", "State");
        Both("Detail.WifiSignal", "信号", "Signal");
        Both("Detail.WifiChannel", "信道", "Channel");
        Both("Detail.WifiRadioType", "无线电类型", "Radio Type");
        Both("Detail.WifiAuth", "身份验证", "Authentication");
        Both("Detail.WifiRx", "接收速率", "Receive Rate");
        Both("Detail.WifiTx", "发送速率", "Transmit Rate");
        Both("Detail.WifiMode", "连接模式", "Connection Mode");

        Both("Peripheral.Unavailable", "无法获取信息", "Unavailable");
        Both("Peripheral.Vid", "VID", "VID");
        Both("Peripheral.Pid", "PID", "PID");
        Both("Peripheral.DriverProvider", "驱动提供者", "Driver Provider");
        Both("Peripheral.DriverVersion", "驱动版本", "Driver Version");
        Both("Peripheral.Service", "服务", "Service");
        Both("Peripheral.DeviceId", "设备 ID", "Device ID");
        Both("Peripheral.Resolution", "分辨率", "Resolution");
        Both("Peripheral.RefreshRate", "刷新率", "Refresh Rate");
        Both("Peripheral.Year", "生产年份", "Year");

        Both("Detail.Uuid", "UUID", "UUID");
        Both("Detail.ProductName", "产品名称", "Product Name");
        Both("Detail.ProductVersion", "产品版本", "Product Version");
        Both("Detail.Vbs", "基于虚拟化的安全 (VBS)", "Virtualization-Based Security (VBS)");
        Both("Detail.MemoryIntegrity", "内存完整性 (HVCI)", "Memory Integrity (HVCI)");
        Both("Detail.CodeIntegrity", "代码完整性策略", "Code Integrity Policy");
        Both("Detail.Vbs.0", "已关闭", "Off");
        Both("Detail.Vbs.1", "已启用（未运行）", "Enabled (not running)");
        Both("Detail.Vbs.2", "正在运行", "Running");
        Both("Detail.CodeIntegrity.0", "已关闭", "Off");
        Both("Detail.CodeIntegrity.1", "审核模式", "Audit Mode");
        Both("Detail.CodeIntegrity.2", "强制执行", "Enforced");
        Both("Detail.ProblemDevices", "问题设备", "Problem Devices");
        Both("Detail.NoProblemDevices", "无问题设备", "No problem devices");
        Both("Detail.DeviceError", "错误代码", "Error Code");
        Both("Detail.Ipv4", "IPv4", "IPv4");
        Both("Detail.Ipv6", "IPv6", "IPv6");
        Both("Detail.Subnet", "子网掩码", "Subnet Mask");
        Both("Detail.DhcpServer", "DHCP 服务器", "DHCP Server");
        Both("Detail.DnsDomain", "DNS 后缀", "DNS Suffix");

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
