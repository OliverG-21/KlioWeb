using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Klio.Core.Interfaces;
using Klio.Infrastructure.Data;
using Klio.Infrastructure.Repositories;
using Klio.Infrastructure.Services;
using Klio.Worker;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=klio;Username=klio;Password=kliodev";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString)
           .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

// Repositories
builder.Services.AddScoped<IImageRepository, ImageRepository>();
builder.Services.AddScoped<ITopicRepository, TopicRepository>();
builder.Services.AddScoped<IAlbumFolderRepository, AlbumFolderRepository>();
builder.Services.AddScoped<IDuplicateLogRepository, DuplicateLogRepository>();
builder.Services.AddScoped<IAppSettingsRepository, AppSettingsRepository>();

// Domain services
builder.Services.AddScoped<IFileOrganizerService, FileOrganizerService>();
builder.Services.AddScoped<IFolderImportService, FolderImportService>();
builder.Services.AddSingleton<IProcessingQueue, ProcessingChannel>();
builder.Services.AddTransient<IExifService, ExifService>();
builder.Services.AddTransient<IThumbnailService, ThumbnailService>();
builder.Services.AddTransient<IHashService, HashService>();

// HTTP client and video services
builder.Services.AddHttpClient();
builder.Services.AddScoped<IVideoThumbnailService, VideoThumbnailService>();

// Tagging Add-On
builder.Services.AddScoped<IImageTagRepository, ImageTagRepository>();
builder.Services.AddScoped<ITaggingService, OllamaTaggingService>();
builder.Services.AddSingleton<ITaggingQueue, TaggingQueue>();

// Background services (worker integrated into web host)
builder.Services.AddHostedService<FileWatcherService>();
builder.Services.AddHostedService<ImageProcessorService>();
builder.Services.AddHostedService<TaggingProcessorService>();

// Cookie authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/admin/login";
        options.LogoutPath = "/admin/logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "KlioAuth";
    });

builder.Services.AddAuthorization();
builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Ensure database is created/migrated
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    var settingsRepo = scope.ServiceProvider.GetRequiredService<IAppSettingsRepository>();
    await settingsRepo.GetAsync();
}

app.UseExceptionHandler("/");
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseStatusCodePagesWithRedirects("/");

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Protect all /admin/* routes (except login/logout) at middleware level
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    var isAdminPath = path.StartsWithSegments("/admin");
    var isPublicAdminPath = path.StartsWithSegments("/admin/login")
                         || path.StartsWithSegments("/admin/logout");

    if (isAdminPath && !isPublicAdminPath && context.User?.Identity?.IsAuthenticated != true)
    {
        var returnUrl = context.Request.Path + context.Request.QueryString;
        context.Response.Redirect("/admin/login?returnUrl=" + Uri.EscapeDataString(returnUrl));
        return;
    }

    await next();
});

app.MapRazorPages();

app.Run();
