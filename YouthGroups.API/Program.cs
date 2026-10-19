using Microsoft.EntityFrameworkCore;
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
builder.Services.AddScoped<BudgetPots>();

// Stateless helpers - one instance for the whole application
builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddSingleton<BudgetCalculator>();

builder.Services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
