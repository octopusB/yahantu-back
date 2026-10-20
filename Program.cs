using Yahantu.PHDiagram;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<DiagramState>();
builder.Services.AddSingleton<ISensorDataSource, DemoSensorDataSource>();
builder.Services.AddSingleton<IRefrigerantPropertyCalculator, CoolPropPropertyCalculator>();
builder.Services.AddHostedService<SamplingWorker>();

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/diagram", (DiagramState state) => Results.Ok(state.GetSnapshot()));
app.MapGet("/api/refrigerants", (IRefrigerantPropertyCalculator calculator) => Results.Ok(calculator.SupportedRefrigerants));
app.MapPost("/api/refrigerant", (SetRefrigerantRequest request, DiagramState state, IRefrigerantPropertyCalculator calculator) =>
{
    if (!calculator.SupportedRefrigerants.Contains(request.Refrigerant, StringComparer.OrdinalIgnoreCase))
        return Results.BadRequest(new { message = "不支持该冷媒。" });

    state.SetRefrigerant(request.Refrigerant);
    return Results.Ok(state.GetSnapshot());
});

app.MapFallbackToFile("index.html");
app.Run();
