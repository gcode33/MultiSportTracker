using MultiSportTracker.Data;
using MultiSportTracker.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// HttpClient for TheSportsDB (using free public key '3')
builder.Services.AddHttpClient<ISportsApiService, SportsApiService>(client =>
{
    client.BaseAddress = new Uri("https://www.thesportsdb.com/api/v1/json/3/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Caching and helper services
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<CacheService>();

// SignalR (optional live push hub)
builder.Services.AddSignalR();

// Optional controllers
builder.Services.AddControllers();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.MapControllers();
app.MapBlazorHub();
app.MapHub<LiveScoreHub>("/hubs/livescores"); // optional hub endpoint
app.MapFallbackToPage("/_Host");

app.Run();
