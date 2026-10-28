using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using HackerToolbox.Data;
using HackerToolbox.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// 1. Provider de banco: Database:Provider = Sqlite (padrão) ou Postgres
var databaseProvider = builder.Configuration["Database:Provider"];
// Postgres: componentes em Database:Postgres:* (senha por arquivo/secret) têm prioridade sobre a connection string completa
var connectionString =
    (DatabaseProviderConfigurator.NormalizeProvider(databaseProvider) == DatabaseProviderConfigurator.Postgres
        ? PostgresConnectionString.Build(k => builder.Configuration["Database:Postgres:" + k])
        : null)
    ?? builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    DatabaseProviderConfigurator.Configure(options, databaseProvider, connectionString));

// 2. Configuração do ASP.NET Core Identity (Usuários e Permissões)
builder.Services.AddDefaultIdentity<IdentityUser>(options => {
    // Regras de senha flexíveis para testes locais
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();


// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.Configure<ShopeeApiOptions>(builder.Configuration.GetSection("Shopee"));
builder.Services.AddSingleton(serviceProvider =>
{
    // The Shopee API requires credentials in the query string; bypass HttpClientFactory URI logging.
    var httpClient = new HttpClient(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = System.Net.DecompressionMethods.All,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5)
    })
    {
        Timeout = TimeSpan.FromSeconds(20)
    };
    return new ShopeeApiClient(
        httpClient,
        serviceProvider.GetRequiredService<IOptions<ShopeeApiOptions>>());
});

// Chaves de Data Protection persistentes (cookies/antiforgery sobrevivem à recriação do container).
// Sem a configuração, mantém o comportamento padrão do ASP.NET Core.
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    builder.Services.AddDataProtection()
        .SetApplicationName("HackerToolbox")
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}

// Forwarded headers somente de proxies explicitamente autorizados (IPs ou CIDRs separados por vírgula).
var trustedProxies = (builder.Configuration["ForwardedHeaders:TrustedProxies"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
if (trustedProxies.Length > 0)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        foreach (var entry in trustedProxies)
        {
            if (entry.Contains('/'))
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(entry));
            else
                options.KnownProxies.Add(System.Net.IPAddress.Parse(entry));
        }
    });
}

builder.Services.AddHealthChecks();

var app = builder.Build();

if (trustedProxies.Length > 0)
{
    app.UseForwardedHeaders();
}

// Seed do administrador (opcional, idempotente): só atua se AdminSeed:Email e AdminSeed:Password estiverem configurados.
// Falhas de configuração/criação interrompem a inicialização de propósito (sem engolir a exceção).
using (var scope = app.Services.CreateScope())
{
    HackerToolbox.Services.AdminSeeder.SeedAsync(
        scope.ServiceProvider,
        builder.Configuration[HackerToolbox.Services.AdminSeeder.EmailSetting],
        ResolveSeedPassword(builder.Configuration),
        scope.ServiceProvider.GetRequiredService<ILogger<Program>>())
        .GetAwaiter().GetResult();
}
// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// O health check do container é HTTP interno e não pode ser redirecionado para HTTPS.
app.UseWhen(ctx => !ctx.Request.Path.StartsWithSegments("/healthz"), branch => branch.UseHttpsRedirection());
app.MapHealthChecks("/healthz");
app.UseStaticFiles(); // Importante para carregar estilos e scripts

app.UseStatusCodePagesWithReExecute("/Error/{0}");

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.UseRouting();

// 3. Middlewares de Segurança (Authentication deve vir SEMPRE antes de Authorization)
app.UseAuthentication(); 
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// 4. Mapeamento das Razor Pages nativas do Identity (Telas de Login / Registro)
app.MapRazorPages();

app.Run();
// Necessário para WebApplicationFactory nos testes de integração.
public partial class Program
{
    // AdminSeed:Password direto, ou AdminSeed:PasswordFile (Docker secret). Arquivo vazio = sem senha configurada.
    private static string? ResolveSeedPassword(IConfiguration config)
    {
        var direct = config[HackerToolbox.Services.AdminSeeder.PasswordSetting];
        var file = config["AdminSeed:PasswordFile"];
        if (!string.IsNullOrEmpty(direct) && !string.IsNullOrWhiteSpace(file))
            throw new InvalidOperationException("Defina AdminSeed:Password ou AdminSeed:PasswordFile, não os dois.");
        return string.IsNullOrWhiteSpace(file) ? direct : HackerToolbox.Data.SecretFile.Read(file);
    }
}
