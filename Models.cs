namespace Yahantu.PHDiagram;

public sealed record SensorReading(string PointName, double TemperatureC, double PressureMpa, DateTimeOffset Timestamp);
public sealed record DiagramPoint(string PointName, double TemperatureC, double PressureMpa, double LogPressure, double EnthalpyKjKg, DateTimeOffset Timestamp);
public sealed record PropertyCurvePoint(double EnthalpyKjKg, double PressureMpa, double LogPressure, double TemperatureC);
public sealed record StandardConditions(double EvaporatingTemperatureC, double CondensingTemperatureC, double SuperheatK, double SubcoolingK);
public sealed record PropertyDiagram(IReadOnlyList<PropertyCurvePoint> SaturatedLiquid, IReadOnlyList<PropertyCurvePoint> SaturatedVapor, IReadOnlyList<DiagramPoint> StandardCycle, StandardConditions Conditions);
public sealed record DiagramSnapshot(string Refrigerant, string PropertyBackend, IReadOnlyList<DiagramPoint> MeasuredPoints, IReadOnlyList<PropertyCurvePoint> SaturatedLiquid, IReadOnlyList<PropertyCurvePoint> SaturatedVapor, IReadOnlyList<DiagramPoint> StandardCycle, StandardConditions Conditions, DateTimeOffset UpdatedAt, string? Error);
public sealed record SetRefrigerantRequest(string Refrigerant);

