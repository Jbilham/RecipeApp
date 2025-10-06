using Microsoft.EntityFrameworkCore;
using RecipeApp.Data;
using RecipeApp.Services;
using RecipeApp.Helpers; // ✅ add this at the top




var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// ✅ Configure DbContext with Postgres
builder.Services.AddDbContext<AppDb>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<MealPlanParser>();
builder.Services.AddScoped<LlmMealPlanParser>();

var app = builder.Build();

// Configure middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();



/*
using Microsoft.EntityFrameworkCore;
using RecipeApp.Data;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// EF Core database (Postgres example)
builder.Services.AddDbContext<AppDb>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "RecipeApp API", Version = "v1" });
});

// Bind OpenAI config from appsettings.json
builder.Services.Configure<OpenAIOptions>(
    builder.Configuration.GetSection("OpenAI"));

var app = builder.Build();

// Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();

app.Run();

// OpenAIOptions record type for strong typing
public class OpenAIOptions
{
    public string ApiKey { get; set; } = string.Empty;
}
*/