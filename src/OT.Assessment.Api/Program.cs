using OT.Assessment.Api.Endpoints;
using OT.Assessment.Api.Middleware;
using OT.Assessment.Application;
using OT.Assessment.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument(options =>
{
    options.Title = "OT Assessment API";
    options.Version = "v1";
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddAssessmentApplication();
builder.Services.AddAssessmentInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapGet("/", () => Results.Redirect("/swagger"));
    app.UseOpenApi(options =>
    {
        options.Path = "/swagger/{documentName}/swagger.json";
    });
    app.UseSwaggerUi(options =>
    {
        options.Path = "/swagger";
        options.DocumentPath = "/swagger/v1/swagger.json";
    });
}

app.UseHttpsRedirection();
app.MapCasinoWagerEndpoints();
app.Run();
