using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;
using WebApplication1.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using static System.Collections.Specialized.BitVector32;

var builder = WebApplication.CreateBuilder(args);

// Agregar servicios al contenedor
builder.Services.AddRazorPages(options =>
{
    // Registrar el filtro de autenticaci�n globalmente
    options.Conventions.AddFolderApplicationModelConvention(
        "/",
        model => model.Filters.Add(new WebApplication1.Models.AuthenticationFilter(
                builder.Services.BuildServiceProvider().GetRequiredService<ILogger<WebApplication1.Models.AuthenticationFilter>>())));
});

// Configurar la sesi�n
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Registrar servicios personalizados
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<AuthService>();

// Configurar conexi�n a la base de datos
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Agregar autenticaci�n personalizada
builder.Services.AddAuthentication()
    .AddCookie(options =>
    {
        options.LoginPath = "/Cliente/Login";
        options.LogoutPath = "/Account/Logout";
    });

// Usar IConfiguration como servicio (si es necesario en otro lugar)
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

var app = builder.Build();

// Configurar el pipeline de solicitudes HTTP
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Usar sesi�n antes del middleware de autenticaci�n
app.UseSession();

// Agregar middleware de autenticaci�n y autorizaci�n
app.UseAuthentication();
app.UseAuthorization();

// Configurar el enrutamiento
app.UseRouting();

// Mapea las Razor Pages
app.MapRazorPages();

app.Run();

