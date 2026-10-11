using KYCSACCO.Data;
using KYCSACCO.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddSignalR();// For API calls if needed
// session for storing verification codes
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Add Authentication services
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        //options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        //options.Cookie.SameSite = SameSiteMode.Strict;
    });

builder.Services.AddAuthorization(options =>
{
    // Add role-based policies
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin", "SuperAdmin"));

    options.AddPolicy("MemberOnly", policy =>
        policy.RequireRole("Member", "Admin", "SuperAdmin"));

    options.AddPolicy("RequireAuthenticatedUser", policy =>
        policy.RequireAuthenticatedUser());

    // ADD THIS POLICY - this is what your controller is using
    options.AddPolicy("Admin", policy =>
        policy.RequireRole("Admin", "SuperAdmin"));
});

// Database Context
var commandTimeout = builder.Configuration.GetValue<int>("CommandTimeout", 1800);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("BlockchainDb"),
        sqlOptions => sqlOptions.CommandTimeout(commandTimeout)));

// Register Application Services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IUserSessionService, UserSessionService>();
builder.Services.AddScoped<ICompanyContextService, CompanyContextService>();
builder.Services.AddScoped<ICompanyService, CompanyService>();
builder.Services.AddScoped<IIpAddressHelper, IpAddressHelper>();
builder.Services.AddScoped<IBlockchainService, BlockchainService>();
builder.Services.AddScoped<ILocationService, LocationService>();

var backgroundServicesEnabled = builder.Configuration.GetValue<bool>("BackgroundServices:Enabled", true);
var transactionInterval = builder.Configuration.GetValue<int>("BackgroundServices:TransactionProcessorIntervalSeconds", 60);
var syncInterval = builder.Configuration.GetValue<int>("BackgroundServices:BlockchainSyncIntervalMinutes", 10);

if (backgroundServicesEnabled && !builder.Environment.IsDevelopment())
{
    builder.Services.AddHostedService<BlockchainSyncService>();
}

// Caching
builder.Services.AddMemoryCache();

// Add logging
builder.Services.AddLogging(configure =>
    configure.AddConsole().AddDebug().SetMinimumLevel(LogLevel.Information));

// SIMPLIFIED Health Checks (without AddDbContextCheck)
builder.Services.AddHealthChecks()
    .AddCheck("Database", () =>
        HealthCheckResult.Healthy("Database connection is healthy"));

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
// app.MapBlazorHub();
// SIMPLIFIED Health check endpoint
app.MapHealthChecks("/health");

// Map controller routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "api",
    pattern: "api/{controller}/{action}/{id?}");

app.Run();