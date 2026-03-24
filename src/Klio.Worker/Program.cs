using Microsoft.EntityFrameworkCore;
using Klio.Core.Interfaces;
using Klio.Infrastructure.Data;
using Klio.Infrastructure.Repositories;
using Klio.Infrastructure.Services;
using Klio.Worker;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=klio;Username=klio;Password=kliodev";

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
