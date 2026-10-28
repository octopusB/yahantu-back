# Yahantu 压焓图

.NET 8 后端仅提供 JSON API，持续获取温度与压力并使用 CoolProp HEOS 状态方程计算压焓点。独立前端位于 `frontend/`，无需 Web 服务器，直接双击 `frontend/index.html` 即可运行。

图中包括：

- CoolProp 计算的饱和液线和饱和蒸汽线（各 121 点）；
- 当前接口数据形成的四点实测循环；
- 标准工况循环：蒸发 5 °C、冷凝 45 °C、过热 5 K、过冷 5 K；
- 标准点采用等熵压缩和等焓节流，表格显示 `T`、绝对压力 `P`、`lg10(P/MPa)` 和比焓 `h`。

## 运行

```powershell
dotnet run
```

后端默认监听地址以终端输出为准。直接打开 `frontend/index.html`，填写后端地址并点击“连接”。页面默认展示 R407C，并可切换 R410A、R134a、R32 和 R22。

前端使用本地 ECharts 文件，支持悬停查看状态点、滚轮缩放、拖动平移、框选缩放、图例开关、还原、导出图片以及手动调整图表宽高。后端已开放 CORS，使 `file://` 打开的页面可以调用接口。

## 安装 CoolProp

项目通过 CoolProp 官方共享库的 C API (`PropsSI`/`Props1SI`) 调用 CoolProp 8：

1. 从 [CoolProp 官方发布页](https://github.com/CoolProp/CoolProp/releases) 获取与系统和进程架构一致的共享库。
2. Windows 将 `CoolProp.dll` 放入 `native/win-x64/`，项目会自动复制到输出和发布目录；也可放入系统 `PATH`。Linux 使用 `libCoolProp.so`，macOS 使用 `libCoolProp.dylib`。
3. 确保应用与原生库架构一致（例如均为 x64），再运行 `dotnet run`。

如果拥有 NIST REFPROP 许可，也可把 `Services.cs` 中的 `HEOS::` 改为 `REFPROP::`，并按 CoolProp 官方 REFPROP 后端说明配置 REFPROP 库路径。REFPROP 本身不随本项目分发。

## 接入真实数据

后端当前每秒读取 `http://10.1.2.80:8080/api/data`，取 `Temperatures` 和 `Pressures` 的前四项，按相同数组下标组成实测点 1–4。接口地址和压力单位位于 `appsettings.json`。

当前按 `bar-gauge`（bar 表压）处理压力，并使用 `AtmosphericPressureBar` 转换为绝对 MPa：

```text
P_abs_MPa = (P_gauge_bar + 1.01325) / 10
```

支持的 `PressureUnit` 配置值为 `bar-gauge`、`bar-absolute`、`mpa-gauge` 和 `mpa-absolute`。确认现场协议后必须选择正确单位；返回值中的 `0.0` 不能直接作为绝对压力用于 `lgP` 和 CoolProp 计算。

当前物性实现是 `CoolPropPropertyCalculator`；若使用厂商服务，只需实现同一个 `IRefrigerantPropertyCalculator` 接口并替换依赖注入注册。

接口压力必须是绝对压力 MPa。如果现场提供表压，需先加当地大气压再传入，否则焓值和 `lgP` 都会错误。物性计算不能替代设备保护与现场安全控制。

## API

- `GET /api/refrigerants`：支持的冷媒列表。
- `POST /api/refrigerant`：设置冷媒，例如 `{ "refrigerant": "R407C" }`。
- `GET /api/diagram`：当前冷媒和最新压焓点。
