using OT.Assessment.Consumer;
using OT.Assessment.Application;
using OT.Assessment.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddAssessmentInfrastructure(builder.Configuration);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
