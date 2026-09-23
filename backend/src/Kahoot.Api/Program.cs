using Kahoot.Api.Authentication;
using Kahoot.Api.Endpoints;
using Kahoot.Api.Middleware;
using Kahoot.Application;
using Kahoot.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
const string corsPolicy = "Frontend";

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddCors(options => options.AddPolicy(corsPolicy, policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddJwtAuthentication(builder.Configuration);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();

app.UseHttpsRedirection();

app.UseRouting();
app.UseCors(corsPolicy);
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapControllers();
app.MapHomePage();

app.Run();
