using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Projekt42.Core.Interfaces;
using Projekt42.Infrastructure.Data;
using Projekt42.Infrastructure.Repositories;
using Projekt42.Infrastructure.Services;
using Projekt42.Worker;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=projekt42;Username=projekt42;Password=projekt42dev";

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
builder.Services.AddSingleton<IProcessingQueue, ProcessingChannel>();
builder.Services.AddTransient<IExifService, ExifService>();
builder.Services.AddTransient<IThumbnailService, ThumbnailService>();
builder.Services.AddTransient<IHashService, HashService>();

// Background services (worker integrated into web host)
builder.Services.AddHostedService<FileWatcherService>();
builder.Services.AddHostedService<ImageProcessorService>();

// Cookie authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/admin/login";
        options.LogoutPath = "/admin/logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "Projekt42Auth";
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
