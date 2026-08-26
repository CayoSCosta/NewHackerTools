using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using HackerToolbox.Data;

var builder = WebApplication.CreateBuilder(args);

// 1. Configuração da String de Conexão com SQLite
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=hackerbox.db";
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

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

var app = builder.Build();

// Bloco Seed Automático Completo (Cria a Role, o Usuário Admin e Associa)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        // 1. Garante que a Role "Admin" existe
        string roleName = "Admin";
        if (!roleManager.RoleExistsAsync(roleName).GetAwaiter().GetResult())
        {
            roleManager.CreateAsync(new IdentityRole(roleName)).GetAwaiter().GetResult();
        }

        // 2. Credenciais do Super Admin vindas de User Secrets / variáveis de ambiente (nunca hardcoded)
        string? adminEmail = builder.Configuration["AdminSeed:Email"];
        string? adminPassword = builder.Configuration["AdminSeed:Password"];

        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogWarning("AdminSeed:Email / AdminSeed:Password não configurados. Seed do Admin ignorado. Configure via 'dotnet user-secrets' (dev) ou variáveis de ambiente (prod).");
        }
        else
        {
            var user = userManager.FindByEmailAsync(adminEmail).GetAwaiter().GetResult();
            if (user == null)
            {
                user = new IdentityUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true
                };

                // Cria o usuário de forma síncrona com a senha padrão
                var createResult = userManager.CreateAsync(user, adminPassword).GetAwaiter().GetResult();
                if (createResult.Succeeded)
                {
                    userManager.AddToRoleAsync(user, roleName).GetAwaiter().GetResult();
                }
            }
            else
            {
                // Se o usuário já existir, apenas garante que ele possui a role Admin
                if (!userManager.IsInRoleAsync(user, roleName).GetAwaiter().GetResult())
                {
                    userManager.AddToRoleAsync(user, roleName).GetAwaiter().GetResult();
                }
            }
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Erro crítico ao gerar o Seed do Administrador.");
    }
}
// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
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