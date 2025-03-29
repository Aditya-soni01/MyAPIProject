using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MyAPIProject.Data;
using MyAPIProject.Middleware;
using Serilog;
using System.Text;
using System.Threading.RateLimiting;

namespace MyAPIProject;

public class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            var builder = WebApplication.CreateBuilder(args);

            ConfigureServices(builder);
            
            var app = builder.Build();
            
            ConfigureMiddleware(app);

            await app.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static void ConfigureServices(WebApplicationBuilder builder)
    {
        // Add Serilog
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("logs/myapp.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        builder.Host.UseSerilog();

        // Add services to the container
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // Add DbContext
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

        var jwtKey = builder.Configuration["Jwt:Key"] ?? 
            throw new InvalidOperationException("Jwt:Key is not configured");
        var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? 
            throw new InvalidOperationException("Jwt:Issuer is not configured");
        var jwtAudience = builder.Configuration["Jwt:Audience"] ?? 
            throw new InvalidOperationException("Jwt:Audience is not configured");

        // Configure JWT Authentication
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtIssuer,
                    ValidAudience = jwtAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtKey))
                };
            });

        // Configure CORS
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowSpecificOrigin",
                builder => builder.WithOrigins("http://localhost:3000")
                                 .AllowAnyMethod()
                                 .AllowAnyHeader());
        });

        // Configure Swagger
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = "My API",
                Version = "v1"
            });
            
            // Configure JWT authentication for Swagger
            c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.",
                Name = "Authorization",
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
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

        // Add RateLimiter
        builder.Services.AddRateLimiter(options => 
        {
            //Configure the rate limiting options
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            //Fixed Window Rate Limiter
            options.AddFixedWindowLimiter("fixed", options => 
            {
                options.PermitLimit = 3; // allow 10 requests
                options.Window = TimeSpan.FromSeconds(10); // per 10 seconds
                options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                options.QueueLimit = 0; // allow 2 requests in the queue
            });
        });
    }


    private static void ConfigureMiddleware(WebApplication app)
    {
        // Always enable Swagger in this demo
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
            c.RoutePrefix = string.Empty;
        });

        // Comment out HTTPS redirection in development
        // app.UseHttpsRedirection();
        

        // Use CORS
        app.UseCors("AllowSpecificOrigin");

        // Add Global Exception Handling Middleware
        app.UseMiddleware<GlobalExceptionMiddleware>();

        // Use Request Logging Middleware
        app.UseMiddleware<RequestLoggingMiddleware>();

        app.UseAuthentication();
        app.UseAuthorization();
        //Use RateLimiter
        app.UseRateLimiter();
        app.MapControllers();

        // Add a default route
        app.MapGet("/", () => Results.Redirect("/swagger"));
    }
}
