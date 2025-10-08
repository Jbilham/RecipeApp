using Microsoft.EntityFrameworkCore;
using RecipeApp.Data;
using RecipeApp.Services;
using RecipeApp.Helpers;
using OpenAI;

var builder = WebApplication.CreateBuilder(args);

// --- Add services to the container ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SupportNonNullableReferenceTypes();
    c.OperationFilter<RecipeApp.Helpers.FileUploadOperationFilter>();
});

// --- PostgreSQL EF Core setup ---
builder.Services.AddDbContext<AppDb>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- Register internal services ---
builder.Services.AddScoped<MealPlanParser>();
builder.Services.AddScoped<LlmMealPlanParser>();
builder.Services.AddScoped<LlmIngredientNormalizer>();

// --- OpenAI setup ---
var apiKey = builder.Configuration["OpenAI:ApiKey"];
Console.WriteLine($"[CONFIG] OpenAI:ApiKey {(string.IsNullOrEmpty(apiKey) ? "NOT FOUND" : "FOUND")} ({apiKey?.Length ?? 0} chars)");
builder.Services.AddSingleton(new OpenAIClient(apiKey));

// --- Build and configure app ---
var app = builder.Build();


// ⭐ Use CORS
app.UseCors("AllowFrontend");

// --- Middleware ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
