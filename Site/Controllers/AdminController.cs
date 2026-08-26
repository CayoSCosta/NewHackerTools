using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HackerToolbox.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AdminController(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // 1. Painel Principal: Listagem de Operadores
        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync();
            var userRoles = new Dictionary<string, IList<string>>();

            foreach (var user in users)
            {
                userRoles[user.Id] = await _userManager.GetRolesAsync(user);
            }

            ViewBag.UserRoles = userRoles;
            ViewBag.Roles = await _roleManager.Roles.ToListAsync();
            return View(users);
        }

        // 2. Tela de Gestão de Grupos/Roles
        public async Task<IActionResult> Roles()
        {
            var roles = await _roleManager.Roles.ToListAsync();
            return View(roles);
        }

        // 3. Ação: Criar Novo Grupo (Role)
        [HttpPost]
        public async Task<IActionResult> CreateRole(string roleName)
        {
            if (!string.IsNullOrWhiteSpace(roleName))
            {
                var formattedName = roleName.Trim();
                if (!await _roleManager.RoleExistsAsync(formattedName))
                {
                    await _roleManager.CreateAsync(new IdentityRole(formattedName));
                    TempData["Success"] = $"Grupo '{formattedName}' criado com sucesso.";
                }
            }
            return RedirectToAction(nameof(Roles));
        }

        // 4. Ação: Excluir Grupo (Role)
        [HttpPost]
        public async Task<IActionResult> DeleteRole(string roleId)
        {
            var role = await _roleManager.FindByIdAsync(roleId);
            if (role != null)
            {
                if (role.Name == "Admin")
                {
                    TempData["Error"] = "O grupo raiz 'Admin' não pode ser destruído.";
                }
                else
                {
                    await _roleManager.DeleteAsync(role);
                    TempData["Success"] = $"Grupo '{role.Name}' removido com sucesso.";
                }
            }
            return RedirectToAction(nameof(Roles));
        }

        // 5. Ação: Atribuir ou Remover Usuário de uma Role específica (Com proteção Root)
        [HttpPost]
        public async Task<IActionResult> ToggleUserRole(string userId, string roleName, bool assign)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                // Impede remover a role Admin do Super Admin principal
                if (user.Email == "admin@hackerbox.net" && roleName == "Admin" && !assign)
                {
                    TempData["Error"] = "Acesso Negado: O Super Admin raiz não pode perder o privilégio Admin.";
                    return RedirectToAction(nameof(Index));
                }

                if (assign)
                {
                    if (!await _userManager.IsInRoleAsync(user, roleName))
                        await _userManager.AddToRoleAsync(user, roleName);
                }
                else
                {
                    if (await _userManager.IsInRoleAsync(user, roleName))
                        await _userManager.RemoveFromRoleAsync(user, roleName);
                }
                TempData["Success"] = "Permissões de grupo atualizadas.";
            }
            return RedirectToAction(nameof(Index));
        }

        // 6. Ação: Banir ou Desbloquear Operador (Lockout)
        [HttpPost]
        public async Task<IActionResult> ToggleBan(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                if (user.Email == "admin@hackerbox.net")
                {
                    TempData["Error"] = "O Super Admin raiz não pode ser banido.";
                    return RedirectToAction(nameof(Index));
                }

                if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow)
                {
                    await _userManager.SetLockoutEndDateAsync(user, null);
                    TempData["Success"] = $"Acesso do operador {user.Email} restaurado.";
                }
                else
                {
                    await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
                    TempData["Success"] = $"Operador {user.Email} banido com sucesso.";
                }
            }
            return RedirectToAction(nameof(Index));
        }

        // 7. Ação: Excluir Operador do Banco
        [HttpPost]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                if (user.Email == "admin@hackerbox.net")
                {
                    TempData["Error"] = "O Super Admin raiz não pode ser expurgado.";
                    return RedirectToAction(nameof(Index));
                }

                await _userManager.DeleteAsync(user);
                TempData["Success"] = "Operador removido permanentemente da base de dados.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}