using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using ClubManagement.Data;
using ClubManagement.Models;
using ClubManagement.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

const string bootstrapArgument = "--bootstrap-staff";
var bootstrap = args.Contains(bootstrapArgument, StringComparer.Ordinal);
var builder = WebApplication.CreateBuilder(args.Where(arg => arg != bootstrapArgument).ToArray());
var config = builder.Configuration;
var connection = config.GetConnectionString("ClubManagement");
if (string.IsNullOrWhiteSpace(connection))
    throw new InvalidOperationException("Configure ConnectionStrings:ClubManagement.");
var jwt = config.GetSection("JwtSettings").Get<JwtSettings>() ?? throw new InvalidOperationException("Missing JwtSettings.");
if (Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32 || jwt.AccessTokenDays <= 0 || jwt.RefreshGraceHours <= 0 ||
    string.IsNullOrWhiteSpace(jwt.Issuer) || string.IsNullOrWhiteSpace(jwt.Audience))
    throw new InvalidOperationException("Configure JwtSettings, including a signing key of at least 32 UTF-8 bytes.");

builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<ClubDbContext>(options => options.UseSqlServer(connection));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<SqlTransactionLock>();
builder.Services.AddScoped<Actor>();
builder.Services.AddScoped<ClubAccess>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ClubService>();
builder.Services.AddScoped<UserAdminService>();
builder.Services.AddScoped<EventProposalService>();
builder.Services.AddScoped<EventService>();
builder.Services.AddScoped<EventAccess>();
builder.Services.AddScoped<EventRegistrationService>();
builder.Services.AddScoped<EventResultService>();
builder.Services.AddScoped<ClubInvitationService>();
builder.Services.AddScoped<ClubTaskService>();
builder.Services.AddScoped<ClubDisciplineService>();
builder.Services.AddScoped<ContributionLedger>();
builder.Services.AddHostedService<ScheduledWork>();
builder.Services.AddScoped<ActivityQueries>();
builder.Services.AddControllers();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("AuthAttempts", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1)
        }));
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Club Management API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Paste the access token from POST /api/auth/login or /api/auth/refresh."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = jwt.Issuer,
        ValidateAudience = true, ValidAudience = jwt.Audience,
        ValidateIssuerSigningKey = true, IssuerSigningKey = jwt.SecurityKey,
        ValidateLifetime = true, ClockSkew = TimeSpan.Zero,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        NameClaimType = "sub", RoleClaimType = "role"
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var userId = context.Principal?.FindFirst("sub")?.Value;
            var sessionId = context.Principal?.FindFirst("sid")?.Value;
            var role = context.Principal?.FindFirst("role")?.Value;
            var db = context.HttpContext.RequestServices.GetRequiredService<ClubDbContext>();
            var now = DateTime.UtcNow;
            var valid = await (from session in db.Set<UserSession>()
                               join user in db.Set<ApplicationUser>() on session.UserId equals user.Id
                               where session.Id == sessionId && user.Id == userId && user.IsActive &&
                                     user.SystemRole == role && session.RevokedAt == null && now < session.AccessExpiresAt
                               select session.Id).AnyAsync(context.HttpContext.RequestAborted);
            if (!valid) context.Fail("Session is inactive.");
        }
    };
});
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy("Staff", policy => policy.RequireRole("Staff"))
    .AddPolicy("Student", policy => policy.RequireRole("Student"));

var app = builder.Build();
if (bootstrap)
{
    using var scope = app.Services.CreateScope();
    var service = scope.ServiceProvider.GetRequiredService<AuthService>();
    await service.BootstrapAsync(config["Bootstrap:Username"], config["Bootstrap:Password"], CancellationToken.None);
    return;
}
app.UseExceptionHandler();
app.UseHttpsRedirection();
if (app.Environment.IsDevelopment() || config.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapOpenApi().AllowAnonymous();
}
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
