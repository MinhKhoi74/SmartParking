using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using SmartParking.Data;
using SmartParking.Data;
using SmartParking.Data.Seed;
using SmartParking.Data.Seed;
using SmartParking.Models;
using SmartParking.Models.Identity;
using SmartParking.Services;
using SmartParking.Services;
using SmartParking.Services.Interfaces;
using SmartParking.Services.Interfaces;
using SmartParking.SignalR;

var builder = WebApplication.CreateBuilder(args);


// ================= DATABASE =================
builder.Services.AddDbContext<ApplicationDBContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


// ================= IDENTITY =================
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;

    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDBContext>()
.AddDefaultTokenProviders();


// ================= JWT =================
var jwtKey = builder.Configuration["JwtSettings:Secret"];

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,

        ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
        ValidAudience = builder.Configuration["JwtSettings:Audience"],

        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtKey))
    };
});


// ================= SERVICES =================
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IBranchService, BranchService>();
builder.Services.AddScoped<IParkingLotService, ParkingLotService>();
builder.Services.AddScoped<IZoneService, ZoneService>();
builder.Services.AddScoped<ISlotService, SlotService>();
builder.Services.AddScoped<IVehicleService, VehicleService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IElectronicTicketService, ElectronicTicketService>();
builder.Services.AddScoped<ICheckInService, CheckInService>();
builder.Services.AddScoped<ICheckOutService, CheckOutService>();
builder.Services.AddHostedService<BookingExpirationService>();
builder.Services.AddScoped<IBranchAuthorizationService, BranchAuthorizationService>();
builder.Services.AddScoped<IVehicleAuthorizationService, VehicleAuthorizationService>();
builder.Services.AddSignalR();

// ================= REDIS =================
var redisConfig = builder.Configuration.GetSection("Redis");
var redisHost = redisConfig["Host"] ?? "localhost";
var redisPort = int.Parse(redisConfig["Port"] ?? "6379");
var redisDb = int.Parse(redisConfig["Database"] ?? "0");
var redisConnectionTimeout = int.Parse(redisConfig["ConnectionTimeout"] ?? "5000");

// Connection string with abortConnect=false to allow retry during migrations
var redisConnectionString = $"{redisHost}:{redisPort},defaultDatabase={redisDb},connectTimeout={redisConnectionTimeout},abortConnect=false";

try
{
    var redis = ConnectionMultiplexer.Connect(redisConnectionString);
    builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
    builder.Services.AddScoped<IRedisService, RedisService>();
}
catch (Exception ex)
{
    // Log warning but continue - allows migrations to work even if Redis is down
    System.Diagnostics.Debug.WriteLine($"⚠️ Warning: Redis connection failed during startup: {ex.Message}");
    // Redis will be retried on first use if connection string has abortConnect=false
}

// ================= CONTROLLERS =================
builder.Services.AddControllers()
.AddJsonOptions(options =>
 {
     options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
     options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
 });
// ================= SWAGGER =================
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter: Bearer your_token"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ================= CORS =================
builder.Services.AddCors(options =>
{
    options.AddPolicy("SignalRPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "https://localhost:3000") // Thay bằng URL Frontend của bạn
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});


var app = builder.Build();


// ================= SEED DATA =================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        await RoleSeeder.SeedRoles(roleManager);
        await UserSeeder.SeedAdminUser(userManager);
    }
    catch (Exception ex)
    {
        // Kiểm tra xem lỗi có hiện ra ở cửa sổ Output/Console không
        Console.WriteLine("Lỗi khi Seed Data: " + ex.Message);
    }
}


// ================= PIPELINE =================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("SignalRPolicy");

app.UseAuthentication();

app.UseAuthorization();

app.MapHub<ParkingHub>("/parkingHub");
app.MapControllers();

app.Run();
