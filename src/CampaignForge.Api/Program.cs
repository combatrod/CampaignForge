using CampaignForge.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:5000");
builder.Host.UseWindowsService();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("CampaignForge")
    ?? throw new InvalidOperationException("Connection string 'CampaignForge' is missing.");

builder.Services.AddDbContext<CampaignDbContext>(options =>
    options.UseSqlServer(connectionString));

var app = builder.Build();

// Existing CampaignForge database is preserved. Schema additions are applied explicitly
// with the reviewed SQL scripts in the package; no automatic schema mutation at startup.

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
