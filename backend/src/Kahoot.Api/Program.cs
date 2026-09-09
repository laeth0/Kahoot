using Kahoot.Application;
using Kahoot.Application.Common.Storage;
using Kahoot.Infrastructure;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();
builder.Services.AddHealthChecks();

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendCorsPolicy", policy =>
    {
        policy.WithOrigins(
                builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ??
                ["http://localhost:5173", "http://localhost:3000"])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var fileStorageOptions = builder.Configuration.GetSection(FileStorageOptions.SectionName).Get<FileStorageOptions>()
                         ?? new FileStorageOptions();

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = fileStorageOptions.MaxSizeBytes;
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseCors("FrontendCorsPolicy");

app.UseHttpsRedirection();

var uploadsRoot = app.Services.GetRequiredService<IOptions<FileStorageOptions>>().Value
    .ResolveRootPath(app.Environment.ContentRootPath);
Directory.CreateDirectory(uploadsRoot);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsRoot),
    RequestPath = fileStorageOptions.NormalizedPublicBasePath,
    ServeUnknownFileTypes = false
});

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
