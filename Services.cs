using System.Collections.Concurrent;

namespace Yahantu.PHDiagram;

public interface ISensorDataSource
{
    Task<IReadOnlyList<SensorReading>> ReadAsync(CancellationToken cancellationToken);
}

public interface IRefrigerantPropertyCalculator
{
    IReadOnlyCollection<string> SupportedRefrigerants { get; }
    double CalculateEnthalpy(string refrigerant, double temperatureC, double pressureMpa);
}

public sealed class DiagramState
{
    private readonly object _sync = new();
    private string _refrigerant = "R410A";
    private IReadOnlyList<DiagramPoint> _points = Array.Empty<DiagramPoint>();
    private DateTimeOffset _updatedAt = DateTimeOffset.MinValue;

    public string GetRefrigerant()
    {
        lock (_sync) return _refrigerant;
    }

    public void SetRefrigerant(string refrigerant)
    {
        lock (_sync)
        {
            _refrigerant = refrigerant.ToUpperInvariant();
            _points = Array.Empty<DiagramPoint>();
            _updatedAt = DateTimeOffset.UtcNow;
        }
    }

    public void Update(IReadOnlyList<DiagramPoint> points)
    {
        lock (_sync)
        {
            _points = points;
            _updatedAt = DateTimeOffset.UtcNow;
        }
    }

    public DiagramSnapshot GetSnapshot()
    {
        lock (_sync) return new DiagramSnapshot(_refrigerant, true, _points, _updatedAt);
    }
}

public sealed class DemoSensorDataSource : ISensorDataSource
{
    private int _sample;

    public Task<IReadOnlyList<SensorReading>> ReadAsync(CancellationToken cancellationToken)
    {
        // TODO: 用已有接口替换此实现。保留 ISensorDataSource 接口不变即可。
        var phase = Interlocked.Increment(ref _sample) * 0.18;
        var now = DateTimeOffset.UtcNow;
        IReadOnlyList<SensorReading> readings = new[]
        {
            new SensorReading("1 吸气", 8 + Math.Sin(phase), 0.72 + Math.Sin(phase) * 0.015, now),
            new SensorReading("2 排气", 76 + Math.Sin(phase * .7) * 2, 2.65 + Math.Sin(phase * .8) * .04, now),
            new SensorReading("3 冷凝器出口", 37 + Math.Sin(phase * .6), 2.58 + Math.Sin(phase * .8) * .04, now),
            new SensorReading("4 蒸发器入口", 5 + Math.Sin(phase * .9), 0.76 + Math.Sin(phase) * .015, now)
        };
        return Task.FromResult(readings);
    }
}

public sealed class DemoRefrigerantPropertyCalculator : IRefrigerantPropertyCalculator
{
    private sealed record Coefficients(double ReferenceEnthalpy, double HeatCapacity, double PressureFactor);

    private static readonly IReadOnlyDictionary<string, Coefficients> Data =
        new Dictionary<string, Coefficients>(StringComparer.OrdinalIgnoreCase)
        {
            ["R410A"] = new(250, 1.62, 18),
            ["R134A"] = new(230, 1.42, 15),
            ["R32"] = new(260, 1.75, 20),
            ["R22"] = new(240, 1.48, 16)
        };

    public IReadOnlyCollection<string> SupportedRefrigerants => Data.Keys.ToArray();

    public double CalculateEnthalpy(string refrigerant, double temperatureC, double pressureMpa)
    {
        if (!Data.TryGetValue(refrigerant, out var c)) throw new ArgumentOutOfRangeException(nameof(refrigerant));
        // 仅供接口和绘图联调的近似式；生产环境应替换为 CoolProp/REFPROP 等已验证物性实现。
        return Math.Round(c.ReferenceEnthalpy + c.HeatCapacity * temperatureC - c.PressureFactor * Math.Log(Math.Max(pressureMpa, .01)), 2);
    }
}

public sealed class SamplingWorker(
    ISensorDataSource sensor,
    IRefrigerantPropertyCalculator calculator,
    DiagramState state,
    ILogger<SamplingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        do
        {
            try
            {
                var refrigerant = state.GetRefrigerant();
                var readings = await sensor.ReadAsync(stoppingToken);
                var points = readings.Select(x => new DiagramPoint(
                    x.PointName,
                    x.TemperatureC,
                    x.PressureMpa,
                    calculator.CalculateEnthalpy(refrigerant, x.TemperatureC, x.PressureMpa),
                    x.Timestamp)).ToArray();
                state.Update(points);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                logger.LogError(ex, "读取传感器或计算压焓点失败");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

