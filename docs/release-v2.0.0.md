AirspyPanel 首个公开发布版，为 Airspy ADS-B SPY 提供中文 Windows 控制面板。

- 自适应 WPF 界面，支持不同窗口尺寸与 DPI。
- 实时飞机列表、报文速率、平均与峰值相对电平。
- 支持自选程序路径，以及 VRS 配置备份、接收机同步和网页来源检查。
- MIT 许可证，源码及构建脚本可用。

下载 `AirspyPanel-v2.0.0-windows.zip` 后完整解压，打开 `AirspyPanel.exe`。需要 .NET Framework 4.8，以及单独下载的 ADS-B SPY；地图功能另需 VRS。第三方程序、DLL 和驱动不包含在包内。

`SHA256SUMS.txt` 可用于校验 ZIP。包内包含中文使用说明和 MIT 许可证。

本次自测及八种离屏布局渲染通过；未重新验证真实硬件接收与实际 VRS 地图端到端流程。信号百分比是 Beast 相对电平，不是校准后的 dBm。
