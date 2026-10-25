using System.Threading.RateLimiting;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Marketplace.Api;
using Marketplace.Core;
using Marketplace.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Marketplace") ?? "Host=localhost;Port=5432;Database=marketplace;Username=marketplace;Password=marketplace;Maximum Pool Size=40;Timeout=3;Command Timeout=3";
builder.Services.AddDbContextPool<MarketplaceDbContext>(options => options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8; options.Password.RequireDigit = true; options.Password.RequireUppercase = true; options.Lockout.MaxFailedAccessAttempts = 5; options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
}).AddEntityFrameworkStores<MarketplaceDbContext>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options => { options.Cookie.Name = "marketplace.auth"; options.Cookie.HttpOnly = true; options.Cookie.SameSite = SameSiteMode.Lax; options.Cookie.SecurePolicy = builder.Environment.IsProduction() ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest; options.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; }; });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AppRoles.Customer, policy => policy.RequireRole(AppRoles.Customer));
    options.AddPolicy(AppRoles.Contractor, policy => policy.RequireRole(AppRoles.Contractor));
});
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("login", limiter =>
    {
        limiter.PermitLimit = 5;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
    options.AddFixedWindowLimiter("write", limiter =>
    {
        limiter.PermitLimit = 30;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});
builder.WebHost.ConfigureKestrel(options => { options.Limits.MaxRequestBodySize = 64 * 1024; options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10); });
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IProfileCache, LruProfileCache>();
builder.Services.AddScoped<ActorResolver>();
builder.Services.AddScoped<ICurrentActor>(services => services.GetRequiredService<ActorResolver>());
builder.Services.AddScoped<IProfileQueries, EfProfileQueries>();
builder.Services.AddScoped<IJobQueries, EfJobQueries>();
builder.Services.AddScoped<IJobCommands, EfJobCommands>();
builder.Services.AddScoped<DatabaseInitializer>();
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["DataProtection:Path"] ?? "/keys"));
var app = builder.Build();
if (args.Contains("--migrate", StringComparer.Ordinal)) { using var scope = app.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(CancellationToken.None); return; }
app.UseExceptionHandler(error => error.Run(async context => { context.Response.StatusCode = 500; await context.Response.WriteAsJsonAsync(new { title = "An unexpected error occurred.", status = 500 }); }));
if (app.Environment.IsProduction()) app.UseHttpsRedirection(); app.UseRateLimiter(); app.UseMiddleware<CsrfMiddleware>(); app.UseAuthentication(); app.UseAuthorization();
app.MapOpenApi(); app.MapScalarApiReference("/scalar"); app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
var api = app.MapGroup("/api/v1");
api.MapGet("/auth/csrf", (HttpContext context) => { var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); context.Response.Cookies.Append("XSRF-TOKEN", token, new CookieOptions { HttpOnly = false, SameSite = SameSiteMode.Lax, Secure = app.Environment.IsProduction(), Path = "/" }); return Results.Ok(new { token }); }).AllowAnonymous();
api.MapPost("/auth/login", async (LoginRequest request, SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 320 ||
        string.IsNullOrWhiteSpace(request.Password) || request.Password.Length is < 8 or > 200)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["credentials"] = ["Supply a valid email address and an 8 to 200 character password."]
        });
    }

    var user = await users.FindByEmailAsync(request.Email);
    if (user is null) return Results.Unauthorized();
    var result = await signIn.PasswordSignInAsync(user, request.Password, true, true);
    return result.Succeeded ? Results.NoContent() : Results.Unauthorized();
}).AllowAnonymous().RequireRateLimiting("login");
api.MapPost("/auth/logout", async (SignInManager<ApplicationUser> signIn) => { await signIn.SignOutAsync(); return Results.NoContent(); }).RequireAuthorization().RequireRateLimiting("write");
api.MapGet("/auth/me", async (HttpContext context, ActorResolver actors, MarketplaceDbContext db, CancellationToken ct) => { var email = context.User.Identity?.Name ?? string.Empty; var customer = await actors.CustomerIdAsync(context.User, ct); if (customer is long customerId) { var profile = await db.Customers.FindAsync([customerId], ct); return Results.Ok(new MeResponse(email, AppRoles.Customer, customerId, $"{profile!.FirstName} {profile.LastName}")); } var contractor = await actors.ContractorIdAsync(context.User, ct); if (contractor is long contractorId) { var profile = await db.Contractors.FindAsync([contractorId], ct); return Results.Ok(new MeResponse(email, AppRoles.Contractor, contractorId, profile!.Name)); } return Results.Unauthorized(); }).RequireAuthorization();
api.MapMarketplaceEndpoints();
app.Run();

public partial class Program { }
