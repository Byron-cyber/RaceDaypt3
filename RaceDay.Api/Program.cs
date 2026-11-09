using System.Reflection;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Common;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

var builder = WebApplication.CreateBuilder(args);


builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        
        options.JsonSerializerOptions.Converters.Add(new TimeOnlyJsonConverter());
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(kv => kv.Value is { Errors.Count: > 0 })
                .ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value!.Errors
                        .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Invalid value." : e.ErrorMessage)
                        .ToArray());
            return new BadRequestObjectResult(new ApiError { Message = "One or more validation errors occurred.", Errors = errors });
        };
    });


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) options.IncludeXmlComments(xmlPath, true);
});


builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


builder.Services.Configure<BlobStorageOptions>(builder.Configuration.GetSection("AzureBlob"));
builder.Services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();


builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();


builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "RaceDay.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
        options.SlidingExpiration = true;

        
        options.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();


using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    if (db.Database.IsRelational())
    {
        
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                
                if (db.Database.GetMigrations().Any()) db.Database.Migrate();
                else db.Database.EnsureCreated();
                break;
            }
            catch (Exception ex) when (attempt < 15)
            {
                app.Logger.LogWarning("Database not ready (attempt {Attempt}/15): {Message}", attempt, ex.Message);
                Thread.Sleep(TimeSpan.FromSeconds(4));
            }
        }
    }
    else
    {
        db.Database.EnsureCreated(); 
    }

    
    if (app.Environment.IsDevelopment())
    {
        DbSeeder.Seed(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>());
    }
}

app.UseSwagger();
app.UseSwaggerUI(options => options.DocumentTitle = "RaceDay API");

app.MapGet("/", () => Microsoft.AspNetCore.Http.Results.Redirect("/swagger")).ExcludeFromDescription();

app.UseAuthentication();   
app.UseAuthorization();
app.MapControllers();

app.Run();


public partial class Program { }
