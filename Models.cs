namespace Yahantu.PHDiagram;

public sealed class SensorApiOptions
{
    public string Url { get; set; } = "http://10.1.2.80:8080/api/data";
    public string PressureUnit { get; set; } = "bar-gauge";
    public double AtmosphericPressureBar { get; set; } = 1.01325;
}

public sealed class SensorApiResponse
{
    public string? Status { get; set; }
    public string? UpdateTime { get; set; }
    public double[]? Temperatures { get; set; }
    public double[]? Pressures { get; set; }
}

public sealed record SensorReading(string PointName, double TemperatureC, double PressureMpa, DateTimeOffset Timestamp);
public sealed record DiagramPoint(string PointName, double TemperatureC, double PressureMpa, double LogPressure, double EnthalpyKjKg, DateTimeOffset Timestamp);
public sealed record PropertyCurvePoint(double EnthalpyKjKg, double PressureMpa, double LogPressure, double TemperatureC);
public sealed record StandardConditions(double EvaporatingTemperatureC, double CondensingTemperatureC, double SuperheatK, double SubcoolingK);
public sealed record PropertyDiagram(IReadOnlyList<PropertyCurvePoint> SaturatedLiquid, IReadOnlyList<PropertyCurvePoint> SaturatedVapor, IReadOnlyList<DiagramPoint> StandardCycle, StandardConditions Conditions);
public sealed record DiagramSnapshot(string Refrigerant, string PropertyBackend, IReadOnlyList<DiagramPoint> MeasuredPoints, IReadOnlyList<PropertyCurvePoint> SaturatedLiquid, IReadOnlyList<PropertyCurvePoint> SaturatedVapor, IReadOnlyList<DiagramPoint> StandardCycle, StandardConditions Conditions, DateTimeOffset UpdatedAt, string? Error);
public sealed record SetRefrigerantRequest(string Refrigerant);
