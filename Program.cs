using CamCook.Services;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// === Config & Credenciales ===
var credentialsPath = builder.Configuration["Google:CredentialsPath"]
    ?? throw new InvalidOperationException("Falta Google:CredentialsPath en user-secrets/appsettings.");
if (!File.Exists(credentialsPath))
    throw new FileNotFoundException($"No existe el archivo de credenciales: {credentialsPath}");

using var fs = new FileStream(credentialsPath, FileMode.Open, FileAccess.Read);
var credential = GoogleCredential.FromStream(fs);

var projectId = builder.Configuration["Google:ProjectId"]
    ?? throw new InvalidOperationException("Falta Google:ProjectId en user-secrets/appsettings.");

// === Firestore ===
var firestore = new FirestoreDbBuilder
{
    ProjectId = projectId,
    Credential = credential
}.Build();
builder.Services.AddSingleton(firestore);

// === Almacenamiento de imágenes LOCAL (gratis) ===
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IImageStorage, LocalImageStorage>();

// === Servicios de tu app ===
builder.Services.AddScoped<IRecipeRepository, RecipeRepository>();
builder.Services.AddRazorPages();

// --- Cookies auth ---
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.LogoutPath = "/Account/Logout";
        o.AccessDeniedPath = "/Account/Login";

        // === NUEVO: calidad de vida ===
        o.ExpireTimeSpan = TimeSpan.FromHours(8);     // sesión dura 8 horas
        o.SlidingExpiration = true;                   // renueva si hay actividad
        // o.Cookie.SameSite = SameSiteMode.Lax;      // (opcional) comportamiento cookie
        // o.Cookie.HttpOnly = true;                  // (opcional) solo accesible por HTTP
        // o.Cookie.SecurePolicy = CookieSecurePolicy.Always; // (opcional) forzar HTTPS
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// === Pipeline HTTP ===
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();       // <- necesario para servir /uploads
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.MapGet("/", (HttpContext ctx) =>
    ctx.User.Identity?.IsAuthenticated == true
        ? Results.Redirect("/Index")
        : Results.Redirect("/Account/Login"));

app.Run();
