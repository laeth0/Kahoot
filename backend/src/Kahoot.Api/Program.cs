using Kahoot.Api.Endpoints;
using Kahoot.Api.HealthChecks;
using Kahoot.Api.Middleware;
using Kahoot.Api.ServiceCollectionExtension;
using Kahoot.Application;
using Kahoot.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
{
    builder.Services.AddControllers();
    builder.Services.AddOpenApi();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();
    builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddAuthorization();

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.InstallServices(
        builder.Configuration,
        typeof(IServiceInstaller).Assembly);
}


var app = builder.Build();
{
    app.UseExceptionHandler();
    app.UseHttpsRedirection();
    app.UseRouting();
    app.UseCors("Frontend");
    app.UseAuthentication();
    app.UseAuthorization();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    app.MapControllers();
    app.MapHealthChecks("/health").AllowAnonymous();
    app.MapHomePage();

    app.Run();
}