namespace Yahantu.PHDiagram;

public sealed record SensorReading(
    string PointName,
    double TemperatureC,
    double PressureMpa,
    DateTimeOffset Timestamp);

public sealed record DiagramPoint(
    string PointName,
    double TemperatureC,
    double PressureMpa,
    double EnthalpyKjKg,
    DateTimeOffset Timestamp);

public sealed record DiagramSnapshot(
    string Refrigerant,
    bool IsDemoCalculation,
    IReadOnlyList<DiagramPoint> Points,
    DateTimeOffset UpdatedAt);

public sealed record SetRefrigerantRequest(string Refrigerant);

