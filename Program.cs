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
.AddEntityFrameworkStores<ApplicationDbContext>();

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // Importante para carregar estilos e scripts

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