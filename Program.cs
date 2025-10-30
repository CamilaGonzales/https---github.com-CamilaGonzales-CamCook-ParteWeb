using System.Text.Json; // Para serializar el objeto del service account a JSON
using CamCook.Services;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

//
// === Config & Credenciales desde appsettings ===
// - Google:ProjectId            -> string (obligatorio)
// - Google:ServiceAccount:*     -> objeto con el JSON del service account (obligatorio)
//

// 1) ProjectId
var projectId = builder.Configuration["Google:ProjectId"]
    ?? throw new InvalidOperationException("Falta Google:ProjectId en appsettings.");

// 2) Objeto ServiceAccount (la sección completa)
var saSection = builder.Configuration.GetSection("Google:ServiceAccount");
if (!saSection.Exists())
    throw new InvalidOperationException("Falta Google:ServiceAccount en appsettings.");

// 3) Bind a un POCO y re-serializar a JSON para dárselo al SDK
var sa = new ServiceAccountOptions();
saSection.Bind(sa);

// Validación mínima
if (string.IsNullOrWhiteSpace(sa.type) ||
    string.IsNullOrWhiteSpace(sa.client_email) ||
    string.IsNullOrWhiteSpace(sa.private_key))
{
    throw new InvalidOperationException("Google:ServiceAccount incompleto: revisa type, client_email y private_key.");
}

// 4) Serializamos a JSON tal cual lo espera GoogleCredential
var saJson = JsonSerializer.Serialize(sa);

// 5) Credencial desde JSON embebido (sin archivo físico)
GoogleCredential credential = GoogleCredential.FromJson(saJson);

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
builder.Services.AddScoped<RecetaService>();
builder.Services.AddSingleton<UsuarioService>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IImageStorage, ImgbbImageStorage>();
builder.Services.AddRazorPages();
// --- Cookies auth ---
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.LogoutPath = "/Account/Logout";
        o.AccessDeniedPath = "/Account/Login";

        // Calidad de vida
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
        // o.Cookie.SameSite = SameSiteMode.Lax;
        // o.Cookie.HttpOnly = true;
        // o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
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
app.UseStaticFiles(); // necesario para servir /uploads
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.MapGet("/", (HttpContext ctx) =>
    ctx.User.Identity?.IsAuthenticated == true
        ? Results.Redirect("/Index")
        : Results.Redirect("/Account/Login"));

app.Run();

//
// POCO para mapear exactamente las claves del Service Account
//
internal sealed class ServiceAccountOptions
{
    public string? type { get; set; }
    public string? project_id { get; set; }
    public string? private_key_id { get; set; }
    public string? private_key { get; set; }
    public string? client_email { get; set; }
    public string? client_id { get; set; }
    public string? auth_uri { get; set; }
    public string? token_uri { get; set; }
    public string? auth_provider_x509_cert_url { get; set; }
    public string? client_x509_cert_url { get; set; }
    public string? universe_domain { get; set; }
}