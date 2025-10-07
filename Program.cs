using Microsoft.EntityFrameworkCore;
using Npgsql;
using RecipeApp.Data;
using RecipeApp.Services;

var builder = WebApplication.CreateBuilder(args);

// ✅ Configure Npgsql global type mapping for dynamic JSON serialization
NpgsqlConnection.GlobalTypeMapper.EnableDynamicJson();

// ✅ Register DbContext
builder.Services.AddDbContext<AppDb>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<LlmMealPlanParser>();

var apiKey = builder.Configuration["OpenAI:ApiKey"];
Console.WriteLine($"[CONFIG] OpenAI:ApiKey {(string.IsNullOrEmpty(apiKey) ? "NOT FOUND" : "FOUND")} ({apiKey?.Length ?? 0} chars)");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
