using System.Runtime.InteropServices;

namespace Yahantu.PHDiagram;

public interface ISensorDataSource { Task<IReadOnlyList<SensorReading>> ReadAsync(CancellationToken cancellationToken); }

public interface IRefrigerantPropertyCalculator
{
    IReadOnlyCollection<string> SupportedRefrigerants { get; }
    string BackendName { get; }
    double CalculateEnthalpy(string refrigerant, double temperatureC, double absolutePressureMpa);
    PropertyDiagram BuildDiagram(string refrigerant);
}

public sealed class DiagramState
{
    private static readonly StandardConditions DefaultConditions = new(5, 45, 5, 5);
    private readonly object _sync = new();
    private string _refrigerant = "R410A";
    private DiagramSnapshot _snapshot = new("R410A", "CoolProp HEOS", Array.Empty<DiagramPoint>(), Array.Empty<PropertyCurvePoint>(), Array.Empty<PropertyCurvePoint>(), Array.Empty<DiagramPoint>(), DefaultConditions, DateTimeOffset.MinValue, "等待 CoolProp 首次计算");
    public string GetRefrigerant() { lock (_sync) return _refrigerant; }
    public void SetRefrigerant(string refrigerant)
    {
        lock (_sync)
        {
            _refrigerant = refrigerant.ToUpperInvariant();
            _snapshot = _snapshot with { Refrigerant = _refrigerant, MeasuredPoints = Array.Empty<DiagramPoint>(), UpdatedAt = DateTimeOffset.UtcNow, Error = "正在重新计算冷媒物性曲线" };
        }
    }
    public void Update(string refrigerant, string backend, IReadOnlyList<DiagramPoint> measured, PropertyDiagram diagram)
    {
        lock (_sync)
        {
            if (!string.Equals(_refrigerant, refrigerant, StringComparison.OrdinalIgnoreCase)) return;
            _snapshot = new(refrigerant, backend, measured, diagram.SaturatedLiquid, diagram.SaturatedVapor, diagram.StandardCycle, diagram.Conditions, DateTimeOffset.UtcNow, null);
        }
    }
    public void SetError(string message) { lock (_sync) _snapshot = _snapshot with { UpdatedAt = DateTimeOffset.UtcNow, Error = message }; }
    public DiagramSnapshot GetSnapshot() { lock (_sync) return _snapshot; }
}

public sealed class DemoSensorDataSource : ISensorDataSource
{
    private int _sample;
    public Task<IReadOnlyList<SensorReading>> ReadAsync(CancellationToken cancellationToken)
    {
        // TODO: 用现场接口替换此实现；压力必须换算为绝对压力 MPa。
        var phase = Interlocked.Increment(ref _sample) * .18;
        var now = DateTimeOffset.UtcNow;
        IReadOnlyList<SensorReading> readings = new[]
        {
            new SensorReading("1 吸气", 10 + Math.Sin(phase), .72 + Math.Sin(phase) * .012, now),
            new SensorReading("2 排气", 76 + Math.Sin(phase * .7) * 2, 2.65 + Math.Sin(phase * .8) * .035, now),
            new SensorReading("3 冷凝器出口", 40 + Math.Sin(phase * .6), 2.58 + Math.Sin(phase * .8) * .035, now),
            new SensorReading("4 蒸发器入口", 5 + Math.Sin(phase * .9), .76 + Math.Sin(phase) * .012, now)
        };
        return Task.FromResult(readings);
    }
}

public sealed class CoolPropPropertyCalculator : IRefrigerantPropertyCalculator
{
    private static readonly string[] Fluids = { "R410A", "R134A", "R32", "R22" };
    public IReadOnlyCollection<string> SupportedRefrigerants => Fluids;
    public string BackendName => "CoolProp HEOS (PropsSI)";
    public double CalculateEnthalpy(string refrigerant, double temperatureC, double absolutePressureMpa) => ToKiloJoules(Props("Hmass", "T", ToKelvin(temperatureC), "P", ToPascal(absolutePressureMpa), Fluid(refrigerant)));

    public PropertyDiagram BuildDiagram(string refrigerant)
    {
        var fluid = Fluid(refrigerant);
        var tripleK = Props1(fluid, "Ttriple");
        var criticalK = Props1(fluid, "Tcrit");
        var liquid = new List<PropertyCurvePoint>();
        var vapor = new List<PropertyCurvePoint>();
        const int samples = 120;
        for (var i = 0; i <= samples; i++)
        {
            var temperatureK = tripleK + 1 + (criticalK * .999 - tripleK - 1) * i / samples;
            liquid.Add(SaturationPoint(fluid, temperatureK, 0));
            vapor.Add(SaturationPoint(fluid, temperatureK, 1));
        }
        var conditions = new StandardConditions(5, 45, 5, 5);
        return new(liquid, vapor, BuildStandardCycle(fluid, conditions), conditions);
    }

    private static IReadOnlyList<DiagramPoint> BuildStandardCycle(string fluid, StandardConditions c)
    {
        var evaporatingK = ToKelvin(c.EvaporatingTemperatureC);
        var condensingK = ToKelvin(c.CondensingTemperatureC);
        var lowPressure = Props("P", "T", evaporatingK, "Q", 1, fluid);
        var highPressure = Props("P", "T", condensingK, "Q", 0, fluid);
        var h1 = Props("Hmass", "T", evaporatingK + c.SuperheatK, "P", lowPressure, fluid);
        var s1 = Props("Smass", "T", evaporatingK + c.SuperheatK, "P", lowPressure, fluid);
        var h2 = Props("Hmass", "P", highPressure, "Smass", s1, fluid);
        var t2 = Props("T", "P", highPressure, "Hmass", h2, fluid);
        var h3 = Props("Hmass", "T", condensingK - c.SubcoolingK, "P", highPressure, fluid);
        var t4 = Props("T", "P", lowPressure, "Hmass", h3, fluid);
        var now = DateTimeOffset.UtcNow;
        return new[] { Point("标准1 压缩机入口", evaporatingK + c.SuperheatK, lowPressure, h1, now), Point("标准2 等熵压缩出口", t2, highPressure, h2, now), Point("标准3 冷凝器出口", condensingK - c.SubcoolingK, highPressure, h3, now), Point("标准4 节流出口", t4, lowPressure, h3, now) };
    }

    private static DiagramPoint Point(string name, double temperatureK, double pressurePa, double enthalpyJkg, DateTimeOffset timestamp)
    {
        var pressureMpa = pressurePa / 1_000_000d;
        return new(name, temperatureK - 273.15, pressureMpa, Math.Log10(pressureMpa), ToKiloJoules(enthalpyJkg), timestamp);
    }
    private static PropertyCurvePoint SaturationPoint(string fluid, double temperatureK, double quality)
    {
        var pressureMpa = Props("P", "T", temperatureK, "Q", quality, fluid) / 1_000_000d;
        var enthalpy = ToKiloJoules(Props("Hmass", "T", temperatureK, "Q", quality, fluid));
        return new(enthalpy, pressureMpa, Math.Log10(pressureMpa), temperatureK - 273.15);
    }
    private static string Fluid(string refrigerant) => "HEOS::" + refrigerant;
    private static double ToKelvin(double value) => value + 273.15;
    private static double ToPascal(double value) => value * 1_000_000d;
    private static double ToKiloJoules(double value) => value / 1000d;
    private static double Props(string output, string input1, double value1, string input2, double value2, string fluid)
    {
        var result = CoolPropNative.PropsSI(output, input1, value1, input2, value2, fluid);
        if (!double.IsFinite(result) || Math.Abs(result) > 1e100) throw new InvalidOperationException($"CoolProp 无法计算 {fluid} 的 {output}。");
        return result;
    }
    private static double Props1(string fluid, string output)
    {
        var result = CoolPropNative.Props1SI(fluid, output);
        if (!double.IsFinite(result) || Math.Abs(result) > 1e100) throw new InvalidOperationException($"CoolProp 无法读取 {fluid} 的 {output}。");
        return result;
    }
    private static class CoolPropNative
    {
        [DllImport("CoolProp", EntryPoint = "PropsSI", CallingConvention = CallingConvention.Cdecl)]
        internal static extern double PropsSI(string output, string input1, double value1, string input2, double value2, string fluid);
        [DllImport("CoolProp", EntryPoint = "Props1SI", CallingConvention = CallingConvention.Cdecl)]
        internal static extern double Props1SI(string fluid, string output);
    }
}

public sealed class SamplingWorker(ISensorDataSource sensor, IRefrigerantPropertyCalculator calculator, DiagramState state, ILogger<SamplingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string? diagramFluid = null;
        PropertyDiagram? diagram = null;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        do
        {
            try
            {
                var refrigerant = state.GetRefrigerant();
                if (!string.Equals(diagramFluid, refrigerant, StringComparison.OrdinalIgnoreCase)) { diagram = calculator.BuildDiagram(refrigerant); diagramFluid = refrigerant; }
                var readings = await sensor.ReadAsync(stoppingToken);
                var points = readings.Select(x => new DiagramPoint(x.PointName, x.TemperatureC, x.PressureMpa, Math.Log10(x.PressureMpa), calculator.CalculateEnthalpy(refrigerant, x.TemperatureC, x.PressureMpa), x.Timestamp)).ToArray();
                state.Update(refrigerant, calculator.BackendName, points, diagram!);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (DllNotFoundException ex) { state.SetError("未找到 CoolProp 原生库。请安装 CoolProp 8 共享库并将其放入程序目录或系统 PATH。 " + ex.Message); logger.LogError(ex, "CoolProp native library not found"); }
            catch (Exception ex) { state.SetError(ex.Message); logger.LogError(ex, "读取传感器或计算压焓图失败"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

