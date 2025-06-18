using BalgImport.Hubs;
using BalgImport.Services;

using Projeto_BALG_Import.Services;

using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.IIS;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
}); 

// Configuração do CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

// Configuração do SignalR
builder.Services.AddSignalR();

// Configuração do Kestrel
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 1024 * 1024 * 1024; // 1GB
});

// Configuração do IIS
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 1024 * 1024 * 1024; // 1GB
});

// Configuração do Form
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 1024 * 1024 * 1024; // 1GB
});

// Registra os serviços
builder.Services.AddScoped<ImportacaoService>();
builder.Services.AddScoped<TestDataGenerator>();
builder.Services.AddSingleton<IStorageService, StorageService>();
builder.Services.AddSingleton<IUploadService, UploadService>();
builder.Services.AddScoped<IImportacaoService, ImportacaoService>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Upload}/{action=Index}/{id?}");

// Mapeia o hub do SignalR
app.MapHub<UploadHub>("/uploadHub");
app.MapHub<ImportacaoHub>("/importacaoHub");
app.MapHub<SignalRHub>("/signalr");

app.Run();

