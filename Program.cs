using ChatBridgeService.Data;
using ChatBridgeService.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("CreatioLocal", policy =>
    {
        var origins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? new[] { "http://localhost:8080", "https://localhost:8443" };

        policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Database
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// Services
builder.Services.AddScoped<IInstanceService, InstanceService>();
builder.Services.AddScoped<IMetaWebhookParser, MetaWebhookParser>();
builder.Services.AddScoped<ICreatioForwarder, CreatioForwarder>();
builder.Services.AddScoped<IMetaMessageSender, MetaMessageSender>();
builder.Services.AddSingleton<CreatioAuthCache>();
builder.Services.AddSingleton<AdminSession>();
builder.Services.AddScoped<ILogService, LogService>();
builder.Services.AddHostedService<AutoCloseChatWorker>();

// HTTP clients
builder.Services.AddHttpClient("creatio", client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddHttpClient("meta", client => client.Timeout = TimeSpan.FromSeconds(15));

var app = builder.Build();

// Auto-migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["Content-Security-Policy"] = "frame-ancestors *";
    await next();
});

app.UseRouting();
app.UseCors("CreatioLocal");
app.MapControllers();

app.Run();
