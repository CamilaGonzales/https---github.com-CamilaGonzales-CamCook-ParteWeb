using System.Text.Json;
using CamCook.Services;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using FirebaseAdmin;

var builder = WebApplication.CreateBuilder(args);

// =====================================================
// === Configuración de Google / Firestore / Firebase ===
// =====================================================
var projectId = builder.Configuration["Google:ProjectId"]
    ?? throw new InvalidOperationException("Falta Google:ProjectId en appsettings.");

var saSection = builder.Configuration.GetSection("Google:ServiceAccount");
if (!saSection.Exists())
    throw new InvalidOperationException("Falta Google:ServiceAccount en appsettings.");

var sa = new ServiceAccountOptions();
saSection.Bind(sa);

if (string.IsNullOrWhiteSpace(sa.type) ||
    string.IsNullOrWhiteSpace(sa.client_email) ||
    string.IsNullOrWhiteSpace(sa.private_key))
{
    throw new InvalidOperationException("Google:ServiceAccount incompleto: revisa type, client_email y private_key.");
}

// Convertimos a JSON y creamos las credenciales
var saJson = JsonSerializer.Serialize(sa);
var credential = GoogleCredential.FromJson(saJson);

// Inicializamos Firestore
var firestore = new FirestoreDbBuilder
{
    ProjectId = projectId,
    Credential = credential
}.Build();
builder.Services.AddSingleton(firestore);

// Inicializamos Firebase Admin
if (FirebaseApp.DefaultInstance == null)
{
    FirebaseApp.Create(new AppOptions
    {
        Credential = credential,
        ProjectId = projectId
    });
}

// =====================================================
//  Límites de Kestrel, formularios y JSON
// =====================================================
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10_000_000;
});

builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 20 * 1024 * 1024;
});

builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.MaxDepth = 64;
});

// =====================================================
//  HttpClient para imgbb y servicios de la app
// =====================================================
builder.Services.AddHttpClient("imgbb", c =>
{
    c.BaseAddress = new Uri("https://api.imgbb.com/");
});
builder.Services.AddSingleton<IImageStorage, ImgbbImageStorage>();

builder.Services.AddScoped<IRecipeRepository, RecipeRepository>();
builder.Services.AddScoped<RecetaService>();
builder.Services.AddSingleton<UsuarioService>();

// =====================================================
//  Razor Pages y Controllers
// =====================================================
builder.Services.AddRazorPages();
builder.Services.AddControllers();

// =====================================================
//  Autenticación con Cookies
// =====================================================
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.Cookie.SameSite = SameSiteMode.None; // <-- para permitir cross-site
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always; // HTTPS obligatorio
    });
builder.Services.AddAuthorization();

// =====================================================
//  Construcción de la app
// =====================================================
var app = builder.Build();

// =====================================================
//  Middleware / Pipeline
// =====================================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

// Redirección raíz
app.MapGet("/", context =>
{
    context.Response.Redirect("/Recipes/List");
    return Task.CompletedTask;
});

app.Run();

// =====================================================
//  Clase interna para ServiceAccountOptions
// =====================================================
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
