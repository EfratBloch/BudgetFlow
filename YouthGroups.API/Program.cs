using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using YouthGroups.Core.Common;
using YouthGroups.Core.Interfaces.Repositories;
using YouthGroups.Core.Interfaces.Services;
using YouthGroups.Core.Settings;
using YouthGroups.Data;
using YouthGroups.Data.Repositories;
using YouthGroups.Data.Seed;
using YouthGroups.Service.Budget;
using YouthGroups.Service.Mapping;
using YouthGroups.Service.Security;
using YouthGroups.Service.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// The connection string comes from configuration (User Secrets in development), never from code
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// Settings from appsettings.json (+ User Secrets for Jwt:Key), read through IOptions<T>
builder.Services.Configure<BudgetSettings>(builder.Configuration.GetSection(BudgetSettings.SectionName));
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));

// Data layer. Scoped = one instance per HTTP request, so every repository in a request
// shares the same AppDbContext and one SaveChangesAsync writes all their changes.
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IParticipantRepository, ParticipantRepository>();
builder.Services.AddScoped<IMeetingRepository, MeetingRepository>();
builder.Services.AddScoped<IEquipmentRepository, EquipmentRepository>();
builder.Services.AddScoped<IBudgetAdditionRepository, BudgetAdditionRepository>();
builder.Services.AddScoped<DbSeeder>();

// Service layer
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IParticipantService, ParticipantService>();
builder.Services.AddScoped<IBudgetService, BudgetService>();
builder.Services.AddScoped<IBudgetAdditionService, BudgetAdditionService>();
builder.Services.AddScoped<IMeetingService, MeetingService>();
builder.Services.AddScoped<IEquipmentService, EquipmentService>();
builder.Services.AddScoped<IGuideOverviewService, GuideOverviewService>();
builder.Services.AddScoped<BudgetPots>();

// Stateless helpers - one instance for the whole application
builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddSingleton<BudgetCalculator>();

builder.Services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());

// JWT authentication: every request may carry "Authorization: Bearer <token>".
// The token is checked against the same Key, Issuer and Audience that AuthService signs with.
var jwt = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();

// Fail at startup, not on the first login, if the secret key is missing or too short.
// User Secrets are read only in the Development environment, so name the environment too:
// running without the http/https launch profile starts in Production and finds no key.
if (Encoding.UTF8.GetByteCount(jwt.Key) < 32)
    throw new InvalidOperationException(
        $"Jwt:Key is missing or shorter than 32 bytes (environment: {builder.Environment.EnvironmentName}). "
        + "In development, start with the http or https launch profile (ASPNETCORE_ENVIRONMENT=Development) "
        + "so User Secrets are loaded, and set the key with: dotnet user-secrets set \"Jwt:Key\" \"<random value>\". "
        + "In production, set the Jwt__Key environment variable.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep our short claim names ("userId", "role") as they are in the token
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1), // the default is 5 minutes of grace after expiry

            // [Authorize(Roles = "Coordinator")] reads this claim
            RoleClaimType = AppClaimTypes.Role,
            NameClaimType = AppClaimTypes.UserId
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Secure by default: every endpoint needs a valid token unless it says [AllowAnonymous]
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Enums travel as text ("Coordinator", "Activity") instead of numbers, in both directions
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Swagger: a test page in the browser (/swagger) generated from the controllers.
// The "Authorize" button sends the JWT with every request made from the page.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "YouthGroups API", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Log in with POST /api/auth/login and paste the token here (without the word Bearer)."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

var app = builder.Build();

// Demo data: switched on by "SeedDemoData": true (appsettings.Development.json).
// DbSeeder only fills an EMPTY database, so restarting the server never duplicates rows.
if (app.Configuration.GetValue<bool>("SeedDemoData"))
{
    // DbSeeder is Scoped (it uses AppDbContext), and there is no HTTP request yet - so make a scope
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // Middleware, not endpoints, so the "login required" fallback policy does not apply to them
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Order matters: first find out who the caller is, then check what she may do
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
