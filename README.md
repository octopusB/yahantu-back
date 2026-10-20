# Yahantu 压焓图

.NET 8 后端持续获取温度与压力，使用 CoolProp HEOS 状态方程计算压焓点；HTML 前端每秒刷新并使用 Canvas 绘制标准 `lgP-h` 图。

图中包括：

- CoolProp 计算的饱和液线和饱和蒸汽线（各 121 点）；
- 当前接口数据形成的四点实测循环；
- 标准工况循环：蒸发 5 °C、冷凝 45 °C、过热 5 K、过冷 5 K；
- 标准点采用等熵压缩和等焓节流，表格显示 `T`、绝对压力 `P`、`lg10(P/MPa)` 和比焓 `h`。

## 运行

```powershell
dotnet run
```

打开终端显示的地址。页面可切换 R410A、R134a、R32 和 R22。

## 安装 CoolProp

项目通过 CoolProp 官方共享库的 C API (`PropsSI`/`Props1SI`) 调用 CoolProp 8：

1. 从 [CoolProp 官方发布页](https://github.com/CoolProp/CoolProp/releases) 获取与系统和进程架构一致的共享库。
2. Windows 将 `CoolProp.dll` 放在应用输出目录或系统 `PATH`；Linux 使用 `libCoolProp.so`，macOS 使用 `libCoolProp.dylib`。
3. 确保应用与原生库架构一致（例如均为 x64），再运行 `dotnet run`。

如果拥有 NIST REFPROP 许可，也可把 `Services.cs` 中的 `HEOS::` 改为 `REFPROP::`，并按 CoolProp 官方 REFPROP 后端说明配置 REFPROP 库路径。REFPROP 本身不随本项目分发。

## 接入真实数据

1. 新建类实现 `ISensorDataSource.ReadAsync`，从已有接口返回各测点的温度（°C）和绝对压力（MPa）。
2. 在 `Program.cs` 中把 `DemoSensorDataSource` 替换为该实现。
3. 当前物性实现是 `CoolPropPropertyCalculator`；若使用厂商服务，只需实现同一个 `IRefrigerantPropertyCalculator` 接口并替换依赖注入注册。

接口压力必须是绝对压力 MPa。如果现场提供表压，需先加当地大气压再传入，否则焓值和 `lgP` 都会错误。物性计算不能替代设备保护与现场安全控制。

## API

- `GET /api/refrigerants`：支持的冷媒列表。
- `POST /api/refrigerant`：设置冷媒，例如 `{ "refrigerant": "R410A" }`。
- `GET /api/diagram`：当前冷媒和最新压焓点。
