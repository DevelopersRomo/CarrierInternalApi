using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using InternalCarrierApp.API.Data;
using InternalCarrierApp.API.Models;
using InternalCarrierApp.API.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Database ───────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<ApplicationDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Identity ───────────────────────────────────────────────────────────────────
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(opt =>
{
    opt.Password.RequireDigit           = true;
    opt.Password.RequiredLength         = 8;
    opt.Password.RequireUppercase       = true;
    opt.Password.RequireNonAlphanumeric = false;
    opt.User.RequireUniqueEmail         = true;

    // Without this, /api/auth/login accepts unlimited password guesses per account.
    opt.Lockout.AllowedForNewUsers      = true;
    opt.Lockout.MaxFailedAccessAttempts = 5;
    opt.Lockout.DefaultLockoutTimeSpan  = TimeSpan.FromMinutes(15);
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// ── JWT ────────────────────────────────────────────────────────────────────────
var jwt = builder.Configuration.GetSection("JwtSettings");

// Environment wins over appsettings so deployments can supply the key without it
// ever living in a tracked file. HS256 needs >= 256 bits of key material.
var jwtSecret = builder.Configuration["JWT_SECRET_KEY"] ?? jwt["SecretKey"];
if (string.IsNullOrWhiteSpace(jwtSecret) || Encoding.UTF8.GetByteCount(jwtSecret) < 32)
{
    throw new InvalidOperationException(
        "JWT signing key missing or shorter than 256 bits. Set JWT_SECRET_KEY or JwtSettings:SecretKey.");
}

builder.Services
    .AddAuthentication(opt =>
    {
        opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        opt.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwt["Issuer"],
            ValidAudience            = jwt["Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(
                                           Encoding.UTF8.GetBytes(jwtSecret)),
            // Default is 5 minutes of leeway on expiry; keep it tight.
            ClockSkew                = TimeSpan.FromSeconds(30)
        };

        // A signed token stays valid until it expires, so without this an admin who
        // deactivates an account (or changes its role) would wait up to
        // ExpiresInHours for it to take effect. Re-check the user on every request.
        opt.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var users  = context.HttpContext.RequestServices
                                    .GetRequiredService<UserManager<ApplicationUser>>();
                var userId = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var user   = userId is null ? null : await users.FindByIdAsync(userId);

                if (user is null || !user.IsActive)
                {
                    context.Fail("Account is no longer active.");
                    return;
                }

                // Identity bumps the security stamp on role and credential changes.
                var stamp = context.Principal?.FindFirst("security_stamp")?.Value;
                if (stamp is not null && stamp != await users.GetSecurityStampAsync(user))
                    context.Fail("Session is stale.");
            }
        };
    });

builder.Services.AddAuthorization();

// ── Rate limiting ──────────────────────────────────────────────────────────────
// Only the anonymous auth endpoints are throttled: they are the ones an attacker
// can hammer without credentials. Partitioned per client IP.
builder.Services.AddRateLimiter(opt =>
{
    opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opt.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window      = TimeSpan.FromMinutes(1),
                QueueLimit  = 0
            }));
});

// ── CORS ────────────────────────────────────────────────────────────────────────
builder.Services.AddCors(opt =>
    opt.AddPolicy("AllowAngular", p =>
        p.WithOrigins(
            "http://localhost:3000",
            "http://localhost:4200",
            "http://161.145.91.108",
            "http://161.145.91.108:80")
         .AllowAnyHeader()
         .AllowAnyMethod()));

// ── Services ───────────────────────────────────────────────────────────────────
builder.Services.AddScoped<ITokenService, TokenService>();

// Novu (in-app inbox + email). A typed client so the handler is pooled instead of a
// new socket per notification.
builder.Services.AddHttpClient<INovuService, NovuService>(client =>
{
    // The relative paths below assume a trailing slash; without it `Uri` would drop
    // the last segment of a base address that has a path.
    var apiUrl = builder.Configuration["Novu:ApiUrl"] ?? "https://api.novu.co";
    client.BaseAddress = new Uri(apiUrl.TrimEnd('/') + "/");

    // Notifications are best effort: a slow provider must not hold a request open.
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ── Swagger with JWT support ───────────────────────────────────────────────────
builder.Services.AddSwaggerGen(opt =>
{
    opt.SwaggerDoc("v1", new OpenApiInfo
    {
        Title   = "InternalCarrierApp API — Carrier México",
        Version = "v1"
    });
    opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "Bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header
    });
    opt.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                    { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ── Build ──────────────────────────────────────────────────────────────────────
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // Tokens travel in the Authorization header; plain HTTP would expose them.
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors("AllowAngular");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ── Seed ───────────────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
    await SeedData.InitializeAsync(scope.ServiceProvider);

app.Run();
