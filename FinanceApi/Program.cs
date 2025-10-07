using FinanceApi.Data; 
using Microsoft.EntityFrameworkCore;
using FinanceApi.Interfaces;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using FinanceApi.Repositories;
using FinanceApi.Services;
using FinanceApi.InterfaceService;


var builder = WebApplication.CreateBuilder(args);

//Add DbContext

builder.Services.AddDbContext<ApplicationDbContext>(options =>
options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

//builder.Services.AddDbContext<ApplicationDbContext>(options =>
//options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));




builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});


// Add JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"]; // pulls from appsettings.json
var key = Encoding.ASCII.GetBytes(jwtSecret ?? throw new Exception("JWT secret not configured"));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // Set to true in production
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false, // Optional: true if you use Issuer
        ValidateAudience = false, // Optional: true if you use Audience
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
});


builder.Services.Scan(scan => scan
    .FromAssemblyOf<ICustomerService>() // or use FromCallingAssembly() if everything is in the same project
    .AddClasses(classes => classes.Where(c => c.Name.EndsWith("Service")))
        .AsImplementedInterfaces()
        .WithScopedLifetime()
    .AddClasses(classes => classes.Where(c => c.Name.EndsWith("Repository")))
        .AsImplementedInterfaces()
        .WithScopedLifetime()
);
builder.Services.AddScoped<IDbConnectionFactory, SqlDbConnectionFactory>();
builder.Services.AddScoped<IPurchaseGoodReceiptRepository, PurchaseGoodReceiptRepository>();
builder.Services.AddScoped<IIncotermsRepository, IncotermsRepository>();
builder.Services.AddScoped<IFlagIssuesRepository, FlagIssuesRepository>();
builder.Services.AddScoped<ISupplierInvoicePinRepository, SupplierInvoicePinRepository>();


builder.Services.AddScoped<IPurchaseGoodReceiptService, PurchaseGoodReceiptService>();
builder.Services.AddScoped<IIncotermsService, IncotermsService>();
builder.Services.AddScoped<IflagIssuesServices, FlagIssuesServices>();
builder.Services.AddScoped<ISupplierInvoicePinService, SupplierInvoicePinService>();
// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

//Cloud Connection
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://*:{port}");
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseHttpsRedirection();
}

// Serve Angular static files from wwwroot
app.UseDefaultFiles();   // looks for index.html
app.UseStaticFiles();     // serve CSS/JS/assets



app.UseCors("AllowAngularApp");

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

// This ensures Angular routes (like /dashboard) work
app.MapFallbackToFile("dist/finance/index.html");

app.Run();
