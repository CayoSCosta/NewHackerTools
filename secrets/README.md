# secrets/

Arquivos de secrets locais usados por `compose.postgres.yaml`. **Somente os `*.example` e este README são versionados**; qualquer outro arquivo desta pasta é ignorado pelo Git e pelo `.dockerignore`.

| Arquivo | Conteúdo |
|---|---|
| `hbx_pg_password.txt` | Senha do PostgreSQL (qualquer caractere é permitido, inclusive espaços e `;`). |
| `admin_seed_password.txt` | Senha inicial do administrador. Arquivo vazio = não criar administrador (deixe também `ADMIN_SEED_EMAIL` vazio). |

O arquivo deve conter só a senha; quebras de linha no final são ignoradas (espaços não). Não é possível terminar a senha com quebra de linha.

## Criar (PowerShell, sem quebra de linha final)

```powershell
$pw = -join ((48..57 + 65..90 + 97..122 + 33,35,37,43,45,61,64 | Get-Random -Count 32) | % { [char]$_ })
[IO.File]::WriteAllText("$PWD\secrets\hbx_pg_password.txt", $pw)
[IO.File]::WriteAllText("$PWD\secrets\admin_seed_password.txt", "<senha-inicial-do-admin>")
icacls secrets /inheritance:r /grant:r "${env:USERNAME}:(OI)(CI)F"   # só o seu usuário acessa
```

## Criar (Linux)

```bash
umask 077
openssl rand -base64 36 | tr -d '\n' > secrets/hbx_pg_password.txt
printf '%s' '<senha-inicial-do-admin>' > secrets/admin_seed_password.txt
chmod 700 secrets; chmod 444 secrets/*.txt   # pasta 700 restringe o acesso; 444 é necessário para os usuários dos containers lerem o bind mount
```

## Limites

Secrets de Compose evitam expor a senha em variáveis de ambiente, `docker inspect` e logs, mas **não protegem contra quem tem acesso privilegiado ao Docker** (pode ler `/run/secrets` dentro dos containers) nem contra quem lê o arquivo no host. Faça backup dos secrets separadamente (gerenciador de senhas); sem a senha do PostgreSQL não há como acessar o volume `hbx-pgdata` existente.
