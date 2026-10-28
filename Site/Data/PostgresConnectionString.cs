using Npgsql;

namespace HackerToolbox.Data
{
    // Monta a connection string PostgreSQL a partir de componentes, usando o NpgsqlConnectionStringBuilder
    // (que faz o escaping de ';', espaços, aspas etc.) em vez de concatenar texto.
    // A senha vem de "Password" ou, preferencialmente, de "PasswordFile" (Docker/Compose secret).
    public static class PostgresConnectionString
    {
        // Retorna null quando "Host" não está definido (o chamador usa então a connection string completa).
        public static string? Build(Func<string, string?> get)
        {
            var host = get("Host");
            if (string.IsNullOrWhiteSpace(host)) return null;

            var password = get("Password");
            var passwordFile = get("PasswordFile");
            if (!string.IsNullOrEmpty(password) && !string.IsNullOrWhiteSpace(passwordFile))
                throw new InvalidOperationException("Defina a senha do PostgreSQL por Password ou por PasswordFile, não pelos dois.");
            if (!string.IsNullOrWhiteSpace(passwordFile))
                password = SecretFile.Read(passwordFile);
            if (string.IsNullOrEmpty(password))
                throw new InvalidOperationException("Senha do PostgreSQL ausente (Password ou PasswordFile).");

            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = host,
                Database = Required(get, "Database"),
                Username = Required(get, "Username"),
                Password = password
            };
            var port = get("Port");
            if (!string.IsNullOrWhiteSpace(port))
            {
                if (!int.TryParse(port, out var p) || p is < 1 or > 65535)
                    throw new InvalidOperationException("Porta do PostgreSQL inválida.");
                builder.Port = p;
            }
            return builder.ConnectionString;
        }

        private static string Required(Func<string, string?> get, string key) =>
            get(key) is { Length: > 0 } v && !string.IsNullOrWhiteSpace(v)
                ? v
                : throw new InvalidOperationException($"Configuração do PostgreSQL ausente: {key}.");
    }

    public static class SecretFile
    {
        // Remove apenas quebras de linha finais (espaços fazem parte da senha). Nunca registra o conteúdo.
        public static string Read(string path)
        {
            if (!File.Exists(path))
                throw new InvalidOperationException($"Arquivo de secret não encontrado: {path}");
            return File.ReadAllText(path).TrimEnd('\r', '\n');
        }
    }
}
