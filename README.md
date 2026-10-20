# Yahantu 压焓图

.NET 8 后端持续获取温度与压力，按手动选择的冷媒计算压焓点；HTML 前端每秒刷新并使用 Canvas 绘制 P-h 循环。

## 运行

```powershell
dotnet run
```

打开终端显示的地址。页面可切换 R410A、R134a、R32 和 R22。

## 接入真实数据

1. 新建类实现 `ISensorDataSource.ReadAsync`，从已有接口返回各测点的温度（°C）和绝对压力（MPa）。
2. 在 `Program.cs` 中把 `DemoSensorDataSource` 替换为该实现。
3. 新建 `IRefrigerantPropertyCalculator` 实现，接入经过验证的 CoolProp、REFPROP 或厂商物性服务，并替换演示计算器。

当前 `DemoRefrigerantPropertyCalculator` 是用于打通前后端的近似公式，不是可靠的冷媒状态方程，不得用于工程计算、设备选型或安全控制。

## API

- `GET /api/refrigerants`：支持的冷媒列表。
- `POST /api/refrigerant`：设置冷媒，例如 `{ "refrigerant": "R410A" }`。
- `GET /api/diagram`：当前冷媒和最新压焓点。

