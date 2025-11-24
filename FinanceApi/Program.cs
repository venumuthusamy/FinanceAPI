using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.InterfaceService;
using FinanceApi.Models;
using FinanceApi.Repositories;
using FinanceApi.Services;
using FluentScheduler;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Data;
using System.Text;
using UnityWorksERP.Finance.AR;

var vuexyPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "dist", "vuexy");
var plainWwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
var resolvedWebRoot = Directory.Exists(vuexyPath) ? vuexyPath : plainWwwroot;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    WebRootPath = resolvedWebRoot
});

// ===================== Services =====================

// EF Core
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Dapper connection factory
builder.Services.AddScoped<IDbConnectionFactory, SqlDbConnectionFactory>();

// Convention scan
builder.Services.Scan(scan => scan
    .FromAssemblyOf<ICustomerService>()
        .AddClasses(c => c.Where(t => t.Name.EndsWith("Repository")))
            .AsImplementedInterfaces()
            .WithScopedLifetime()
        .AddClasses(c => c.Where(t => t.Name.EndsWith("Service")))
            .AsImplementedInterfaces()
            .WithScopedLifetime()
);

// Explicit registrations (your existing)
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
builder.Services.AddScoped<IDeliveryOrderRepository, DeliveryOrderRepository>();
builder.Services.AddScoped<ISalesInvoiceRepository, SalesInvoiceRepository>();
builder.Services.AddScoped<IPurchaseAlertRepository, PurchaseAlertRepository>();
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
builder.Services.AddScoped<IDeliveryOrderService, DeliveryOrderService>();
builder.Services.AddScoped<IRunningNumberRepository, RunningNumberRepository>();
builder.Services.AddScoped<IPickingRepository, PickingRepository>();
builder.Services.AddScoped<IPickingService, PickingService>();
builder.Services.AddSingleton<ICodeImageService, CodeImageService>();
builder.Services.AddScoped<ISalesInvoiceService, SalesInvoiceService>();
builder.Services.AddScoped<IPurchaseAlertService, PurchaseAlertService>();
builder.Services.AddScoped<IArReceiptRepository, ArReceiptRepository>();
builder.Services.AddScoped<IArReceiptService, ArReceiptService>();

// (Optional) Explicit Journal DI – scan already covers it, but safe to add:
builder.Services.AddScoped<IJournalRepository, JournalRepository>();
builder.Services.AddScoped<IJournalService, JournalService>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS
const string CorsPolicy = "Frontend";
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: CorsPolicy, policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// JWT (your existing config)
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
        options.RequireHttpsMetadata = false;
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

// PORT binding
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://*:{port}");
}

// Local listen
builder.WebHost.ConfigureKestrel(o => o.ListenAnyIP(7182));

var app = builder.Build();

// ===================== Middleware =====================

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();

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
// app.UseAuthorization(); // enable if using [Authorize]

app.MapControllers();
app.MapFallbackToFile("index.html");

// ===================== FluentScheduler – Recurring Journal Job =====================

JobManager.Initialize();

var scopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();

JobManager.AddJob(
    () =>
    {
        Task.Run(async () =>
        {
            using var scope = scopeFactory.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            var journalService = scope.ServiceProvider.GetRequiredService<IJournalService>();

            try
            {
                // Default ERP timezone (can change later)
                var defaultTimezone = "Asia/Kolkata";
                var tz = TimeZoneInfo.FindSystemTimeZoneById(defaultTimezone);
                var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

                await journalService.ProcessRecurringAsync(nowLocal, defaultTimezone);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in recurring Journal job");
            }
        });
    },
    s => s.ToRunEvery(1).Minutes()   // TEST: every 1 minute
    // PROD: s => s.ToRunEvery(1).Days().At(23, 0)
);

app.Run();
