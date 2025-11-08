// Program.cs
using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;

using FinanceApi.Data;
using FinanceApi.InterfaceService;
using FinanceApi.Interfaces;
using FinanceApi.Repositories;
using FinanceApi.Services;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;

// -------- Pick the correct web root (Angular UI location) --------
var vuexyPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "dist", "vuexy");
var plainWwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
var resolvedWebRoot = Directory.Exists(vuexyPath) ? vuexyPath : plainWwwroot;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    WebRootPath = resolvedWebRoot, // if vuexy exists, use it; else fallback to wwwroot
    // ContentRootPath = AppContext.BaseDirectory // (default is fine)
});

// ===================== Services =====================

// --- Database (SQL Server via EF Core) ---
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- If you use Dapper via a connection factory (keep if your code expects it) ---
builder.Services.AddScoped<IDbConnectionFactory, SqlDbConnectionFactory>();

// --- Repositories / Services (convention scan) ---
builder.Services.Scan(scan => scan
    .FromAssemblyOf<ICustomerService>()
        .AddClasses(c => c.Where(t => t.Name.EndsWith("Repository"))).AsImplementedInterfaces().WithScopedLifetime()
        .AddClasses(c => c.Where(t => t.Name.EndsWith("Service"))).AsImplementedInterfaces().WithScopedLifetime()
);

// Explicit registrations (keep the ones you actually use)
builder.Services.AddScoped<IPurchaseGoodReceiptRepository, PurchaseGoodReceiptRepository>();
builder.Services.AddScoped<ICatagoryRepository, CatagoryRepository>();
builder.Services.AddScoped<IcostingMethodRepository, CostingMethodRepository>();
builder.Services.AddScoped<IIncotermsRepository, IncotermsRepository>();
builder.Services.AddScoped<IFlagIssuesRepository, FlagIssuesRepository>();
builder.Services.AddScoped<ISupplierInvoicePinRepository, SupplierInvoicePinRepository>();
builder.Services.AddScoped<IPurchaseRequestTempRepository, PurchaseRequestTempRepository>();
builder.Services.AddScoped<IStockIssuesRepository, StockIssuesRepository>();
builder.Services.AddScoped<IBinRepository, BinRepository>();
builder.Services.AddScoped<IQuotationRepository, QuotationRepository>();
builder.Services.AddScoped<IStockAdjustmentRepository, StockAdjustmentRepository>();

builder.Services.AddScoped<IPurchaseGoodReceiptService, PurchaseGoodReceiptService>();
builder.Services.AddScoped<IIncotermsService, IncotermsService>();
builder.Services.AddScoped<IflagIssuesServices, FlagIssuesServices>();
builder.Services.AddScoped<ISupplierInvoicePinService, SupplierInvoicePinService>();
builder.Services.AddScoped<IPurchaseRequestTempService, PurchaseRequestTempService>();
builder.Services.AddScoped<ICatagoryService, CatagoryServices>();
builder.Services.AddScoped<IStockIssueServices, StockIssuesServices>();
builder.Services.AddScoped<IBinServices, BinServices>();
builder.Services.AddScoped<IStockAdjustmentServices, StockAdjustmentServices>();
builder.Services.AddScoped<IQuotationService, QuotationService>();

builder.Services.AddHttpContextAccessor();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- CORS ---
const string CorsPolicy = "Frontend";
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: CorsPolicy, policy =>
    {
        // Same-origin doesn't need CORS, but this policy also allows dev split-ports.
        policy
            .WithOrigins(
                "http://localhost:4200",
                "http://localhost:5000",
                "http://localhost:51898"
            //,"https://your-prod-ui-domain.com"

            // add your real UI domain if needed
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
        // If you use cookie auth: .AllowCredentials();  (then remove localhost:5000 if same-origin)
    });
});

// --- JWT (optional; only if you set Jwt:Secret in appsettings) ---
var jwtSecret = builder.Configuration["Jwt:Secret"];
if (!string.IsNullOrWhiteSpace(jwtSecret))
{
    var key = Encoding.ASCII.GetBytes(jwtSecret);
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false; // set true behind HTTPS in prod
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            ValidateIssuer = false,
            ValidateAudience = false,
            IssuerSigningKey = new SymmetricSecurityKey(key),
        };
    });

    builder.Services.AddAuthorization();
}

// --- Cloud dyno port binding (Heroku/Render/Azure AppService etc.) ---
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://*:{port}");
}

var app = builder.Build();

// ===================== Middleware =====================

// Swagger – enable in Dev; optionally enable in Prod too
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}
// else
// {
//     app.UseSwagger();
//     app.UseSwaggerUI();
// }

// Serve static files for the Angular app
app.UseStaticFiles();

// If you also want to expose the plain wwwroot when vuexy is used:
if (resolvedWebRoot == vuexyPath && Directory.Exists(plainWwwroot))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(plainWwwroot),
        RequestPath = ""
    });
}

app.UseRouting();

app.UseCors(CorsPolicy);

app.UseAuthentication();
// app.UseAuthorization(); // uncomment if you use [Authorize] anywhere

// Map API FIRST (so /api/* doesn't get swallowed by SPA fallback)
app.MapControllers();

// Then SPA fallback (serves index.html)
app.MapFallbackToFile("index.html");

app.Run();
