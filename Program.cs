using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "API App Placeholder",
        Version = "v1"
    });
});

var app = builder.Build();

// Support both local /application testing and App Service virtual application mounting.
app.Use((context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/application", out var remainingPath))
    {
        context.Request.PathBase = context.Request.PathBase.Add("/application");
        context.Request.Path = remainingPath;
    }

    return next();
});

app.UseSwagger(options =>
{
    options.RouteTemplate = "swagger/{documentName}/swagger.json";
});

app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "swagger";
    options.SwaggerEndpoint("/application/swagger/v1/swagger.json", "API App Placeholder v1");
});

app.MapGet("/", () =>
    Results.Text(
        $"This data was returned from API App - {Guid.NewGuid()}",
        "text/plain"))
    .WithName("GetApplicationPlaceholder");

app.Run();
