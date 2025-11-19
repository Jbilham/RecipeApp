using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Data;
using RecipeApp.Models;
using RecipeApp.Services;
using RecipeApp.Helpers;
using OpenAI;

var builder = WebApplication.CreateBuilder(args);


// ⭐ Add CORS configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins
            ("http://localhost:5173", 
            "http://localhost:5174") // React dev server
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// --- Add services to the container ---
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SupportNonNullableReferenceTypes();
    c.OperationFilter<RecipeApp.Helpers.FileUploadOperationFilter>();
});

// --- PostgreSQL EF Core setup ---
var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection");
try
{
    var csb = new Npgsql.NpgsqlConnectionStringBuilder(defaultConnection);
    Console.WriteLine("[CONFIG] DefaultConnection => Host={0}; Port={1}; Database={2}; Username={3}",
        csb.Host,
        csb.Port,
        string.IsNullOrWhiteSpace(csb.Database) ? "<none>" : csb.Database,
        string.IsNullOrWhiteSpace(csb.Username) ? "<none>" : csb.Username);
}
catch (Exception ex)
{
    Console.WriteLine($"[CONFIG] DefaultConnection (raw) => {defaultConnection ?? "<null>"}");
    Console.WriteLine($"[CONFIG] Failed to parse DefaultConnection: {ex.Message}");
}

builder.Services.AddDbContext<AppDb>(options =>
    options.UseNpgsql(defaultConnection));

builder.Services.AddIdentityCore<AppUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireDigit = false;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDb>()
    .AddSignInManager();

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContext, UserContext>();

// --- Register internal services ---
builder.Services.AddScoped<MealPlanParser>();
builder.Services.AddScoped<LlmMealPlanParser>();
builder.Services.AddScoped<LlmIngredientNormalizer>();
builder.Services.AddScoped<ShoppingListBuilder>();
builder.Services.AddScoped<MealPlanAssembler>();
builder.Services.AddScoped<ICalendarImportService, CalendarImportService>();

// --- OpenAI setup ---
var apiKey = builder.Configuration["OpenAI:ApiKey"];
Console.WriteLine($"[CONFIG] OpenAI:ApiKey {(string.IsNullOrEmpty(apiKey) ? "NOT FOUND" : "FOUND")} ({apiKey?.Length ?? 0} chars)");
builder.Services.AddSingleton(new OpenAIClient(apiKey));

// --- Build and configure app ---
var app = builder.Build();

await app.SeedIdentityAsync();


// ⭐ Use CORS
app.UseCors("AllowFrontend");

// --- Middleware ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();
