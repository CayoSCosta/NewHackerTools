using System.Net.Mail;
using Microsoft.AspNetCore.Identity;

namespace HackerToolbox.Services
{
    public enum AdminSeedOutcome
    {
        NotConfigured,
        Created,
        AlreadyExisted
    }

    public sealed class AdminSeedException : InvalidOperationException
    {
        public AdminSeedException(string message) : base(message) { }
    }

    // Seed do primeiro administrador. Não há credenciais padrão: sem e-mail e senha configurados nada é criado.
    // Nunca redefine a senha de um usuário existente e nunca escreve a senha em logs ou mensagens de erro.
    public static class AdminSeeder
    {
        public const string AdminRole = "Admin";
        public const string EmailSetting = "AdminSeed:Email";
        public const string PasswordSetting = "AdminSeed:Password";

        public static async Task<AdminSeedOutcome> SeedAsync(
            IServiceProvider services, string? email, string? password, ILogger logger)
        {
            email = email?.Trim();
            var hasEmail = !string.IsNullOrWhiteSpace(email);
            var hasPassword = !string.IsNullOrWhiteSpace(password);

            if (!hasEmail && !hasPassword)
            {
                logger.LogWarning(
                    "Seed do administrador ignorado: {EmailSetting} e {PasswordSetting} não configurados. Nenhum usuário foi criado.",
                    EmailSetting, PasswordSetting);
                return AdminSeedOutcome.NotConfigured;
            }

            if (hasEmail != hasPassword)
                throw new AdminSeedException(
                    $"Configuração incompleta do seed do administrador: defina {EmailSetting} e {PasswordSetting} juntos (ou nenhum dos dois).");

            if (!MailAddress.TryCreate(email, out var parsed) || !string.Equals(parsed.Address, email, StringComparison.Ordinal))
                throw new AdminSeedException($"{EmailSetting} não é um endereço de e-mail válido.");

            var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

            if (!await roleManager.RoleExistsAsync(AdminRole))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole(AdminRole));
                // Outra instância pode ter criado a role entre a checagem e a criação.
                if (!roleResult.Succeeded && !await roleManager.RoleExistsAsync(AdminRole))
                    throw new AdminSeedException($"Falha ao criar a role '{AdminRole}': {Describe(roleResult)}");
            }

            var user = await userManager.FindByEmailAsync(email!);
            var outcome = AdminSeedOutcome.AlreadyExisted;

            if (user == null)
            {
                user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
                var createResult = await userManager.CreateAsync(user, password!);
                if (!createResult.Succeeded)
                    throw new AdminSeedException(
                        $"Falha ao criar o usuário administrador (verifique a política de senha): {Describe(createResult)}");
                outcome = AdminSeedOutcome.Created;
            }

            if (!await userManager.IsInRoleAsync(user, AdminRole))
            {
                var roleAssign = await userManager.AddToRoleAsync(user, AdminRole);
                if (!roleAssign.Succeeded)
                    throw new AdminSeedException($"Falha ao atribuir a role '{AdminRole}' ao administrador: {Describe(roleAssign)}");
            }

            logger.LogInformation(
                outcome == AdminSeedOutcome.Created
                    ? "Usuário administrador criado e associado à role {Role}."
                    : "Usuário administrador já existia; senha mantida e role {Role} garantida.",
                AdminRole);
            return outcome;
        }

        // Apenas códigos/descrições do Identity, que não incluem a senha informada.
        private static string Describe(IdentityResult result) =>
            string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
    }
}
