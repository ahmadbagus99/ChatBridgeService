using ChatBridgeService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddScoped<IMetaWebhookParser, MetaWebhookParser>();
builder.Services.AddHttpClient<ICreatioForwarder, CreatioForwarder>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient<IMetaMessageSender, MetaMessageSender>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});

var app = builder.Build();

app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["Content-Security-Policy"] = "frame-ancestors *";
    await next();
});

app.UseRouting();
app.MapControllers();

app.Run();
