using Modules.Sales;

var builder = WebApplication.CreateBuilder(args);

const string PosCorsPolicy = "PosTablets";

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// The Capacitor WebView serves the Angular bundle from its own origin
// (capacitor://localhost on iOS, http(s)://localhost on Android), so the
// API must explicitly allow those origins.
builder.Services.AddCors(options => options.AddPolicy(PosCorsPolicy, policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddSalesModule(
    builder.Configuration.GetConnectionString("SalesDb")
        ?? throw new InvalidOperationException("Connection string 'SalesDb' is not configured."));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors(PosCorsPolicy);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapSalesModule();

app.Run();
