using Microsoft.EntityFrameworkCore;
using Projekt42.Core.Interfaces;
using Projekt42.Infrastructure.Data;
using Projekt42.Infrastructure.Repositories;
using Projekt42.Infrastructure.Services;
using Projekt42.Worker;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=projekt42;Username=projekt42;Password=projekt42dev";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Repositories (scoped - per request/scope)
builder.Services.AddScoped<IImageRepository, ImageRepository>();
builder.Services.AddScoped<ITopicRepository, TopicRepository>();
builder.Services.AddScoped<IDuplicateLogRepository, DuplicateLogRepository>();
builder.Services.AddScoped<IAppSettingsRepository, AppSettingsRepository>();

// Services (scoped for those that need DbContext)
builder.Services.AddScoped<IFileOrganizerService, FileOrganizerService>();

// Services (singleton/transient as appropriate)
builder.Services.AddSingleton<IProcessingQueue, ProcessingChannel>();
builder.Services.AddTransient<IExifService, ExifService>();
builder.Services.AddTransient<IThumbnailService, ThumbnailService>();
builder.Services.AddTransient<IHashService, HashService>();

// Background services
builder.Services.AddHostedService<FileWatcherService>();
builder.Services.AddHostedService<ImageProcessorService>();

var host = builder.Build();
host.Run();
