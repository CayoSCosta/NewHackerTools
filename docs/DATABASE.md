# Banco de dados: SQLite e PostgreSQL (fase 2)

Estado: **SQLite continua sendo o padrão**. PostgreSQL está preparado no código e nas migrations, mas **não** está no `compose.yaml` e não há migração de dados. A aplicação só foi iniciada contra um PostgreSQL descartável (login/Admin/encurtador reais não foram exercitados).

## Seleção do provider

| Configuração | Valores | Padrão |
|---|---|---|
| `Database:Provider` (env: `Database__Provider`) | `Sqlite`, `Postgres` | `Sqlite` |
| `ConnectionStrings:DefaultConnection` (env: `ConnectionStrings__DefaultConnection`) | connection string | SQLite: `Data Source=hackerbox.db`; Postgres: **obrigatória** |

Um valor inválido de provider falha na inicialização. A lógica fica em `Site/Data/DatabaseProviderConfigurator.cs`.
A senha do PostgreSQL nunca deve ir para arquivos versionados: use variável de ambiente, `dotnet user-secrets` ou segredos do Compose (fase futura).

## Onde ficam as migrations

- **SQLite (históricas, não editar):** `Site/Migrations/` (`InitialIdentity`, `AddShortUrlsTable` e o snapshot).
- **PostgreSQL:** projeto separado `Migrations.Postgres/` (assembly `HackerToolbox.Migrations.Postgres`), com snapshot próprio. É um projeto à parte porque o EF descobre todas as migrations de um assembly para o mesmo contexto; assim os dois históricos não se misturam.

O projeto web **não** referencia `Migrations.Postgres` (ele é que referencia o web). Em vez disso, `dotnet publish Site/HackerToolbox.csproj` compila e copia `HackerToolbox.Migrations.Postgres.dll` para a saída (alvo `IncludePostgresMigrationsInPublish`); o `Dockerfile` faz o mesmo. Com `Database:Provider=Postgres` o assembly é carregado de `AppContext.BaseDirectory` (não consta no `deps.json`); se faltar, a aplicação falha com mensagem clara. O DLL é copiado para a saída por alvos MSBuild do `Site` (após `Build` e após `Publish`), sem referência circular; veja "Fluxo de desenvolvimento". O web **não** aplica migrations: o schema é criado por script SQL idempotente ou `dotnet ef database update` explícito. `Migrations.Postgres` consta em `Site/HackerToolbox.slnx`.

## Comandos

Execute sempre na raiz do repositório. O `dotnet ef` usa as variáveis `HBX_DB_PROVIDER` e `HBX_DB_CONNECTION` apenas em design-time.

### SQLite (migrations em `Site/`)

```powershell
# Listar sem conectar ao banco
dotnet ef migrations list --no-connect --project Site --startup-project Site

# Criar nova migration SQLite (não conecta ao banco; não altera as existentes)
dotnet ef migrations add NomeDaMigration --project Site --startup-project Site

# Aplicar (ATENÇÃO: sem HBX_DB_CONNECTION, usa Site\hackerbox.db relativo ao diretório atual)
$env:HBX_DB_CONNECTION = "Data Source=C:\caminho\banco-de-teste.db"
dotnet ef database update --project Site --startup-project Site
```

### PostgreSQL (migrations em `Migrations.Postgres/`)

```powershell
# A connection string abaixo é só para design-time offline (gerar migration não conecta)
$env:HBX_DB_CONNECTION = "Host=localhost;Database=design;Username=design;Password=<qualquer>"

# Criar nova migration
dotnet ef migrations add NomeDaMigration --project Migrations.Postgres --startup-project Migrations.Postgres --output-dir Migrations

# Gerar script SQL idempotente (recomendado para revisão e aplicação)
dotnet ef migrations script --idempotent --project Migrations.Postgres --startup-project Migrations.Postgres -o initial.sql

# Aplicar a um PostgreSQL DESCARTÁVEL/de ensaio (nunca a um banco com dados reais sem aprovação)
$env:HBX_DB_CONNECTION = "Host=<host>;Port=5432;Database=<db>;Username=<user>;Password=<senha>"
dotnet ef database update --project Migrations.Postgres --startup-project Migrations.Postgres
```

Ensaio sem publicar porta no host (container descartável, dados em memória):

```powershell
$pw = [guid]::NewGuid().ToString('N')
docker run -d --rm --name hbx-pg-scratch -e POSTGRES_PASSWORD=$pw -e POSTGRES_DB=scratch --tmpfs /var/lib/postgresql/data postgres:17
Get-Content initial.sql -Raw | docker exec -i hbx-pg-scratch psql -U postgres -d scratch -v ON_ERROR_STOP=1
docker stop hbx-pg-scratch   # --rm remove o container e os dados
```

## Datas: UTC (decidido) e conversão de registros antigos (PENDENTE)

- Novos `ShortUrl.CreatedAt` são gravados com `DateTime.UtcNow` (`Kind=Utc`) em qualquer provider; PostgreSQL usa `timestamp with time zone` (`InitialPostgres` foi regerada e confirma esse tipo).
- No SQLite o valor continua sendo `TEXT` sem fuso, então registros novos (UTC) e antigos (horário local gravado por `DateTime.Now`) passam a coexistir no mesmo banco. Nada foi convertido.

### Comportamento por provider (comprovado nos testes)

| | SQLite | PostgreSQL |
|---|---|---|
| Armazenamento | `TEXT` sem fuso (o relógio UTC gravado) | `timestamp with time zone` (instante absoluto) |
| `Kind` ao ler | `Unspecified` | `Utc` |
| `Kind=Local`/`Unspecified` na gravação | aceito (sem fuso) | **rejeitado** pelo Npgsql (exceção) |
| Efeito do `TimeZone` da sessão | n/a | nenhum sobre o instante (testado com `UTC` e `Asia/Tokyo`) |

### Exibição em Brasília

`Site/Services/BrasiliaTime.cs` converte apenas na exibição (`Shortener/Index`), nunca altera o valor armazenado: `Utc` é convertido para `America/Sao_Paulo` (fallback `E. South America Standard Time` no Windows); `Unspecified` (SQLite) é **assumido como UTC**; `Local` é convertido via UTC. Há uma única conversão (UTC → Brasília), sem dupla conversão.

Consequência no SQLite: os registros antigos (horário de Brasília gravado por `DateTime.Now`, presumivelmente) serão exibidos 3 h **atrasados** (ex.: 15:15 aparece como 12:15), pois são lidos como UTC, até a regra de conversão ser definida. Isso não afeta o PostgreSQL com dados novos.

### Registros antigos do SQLite: investigação e proposta (NÃO aplicada; PENDENTE de confirmação)

Observado em cópia consistente e somente leitura do banco real (o original não foi alterado):

| Id | Code | Tipo SQLite | `CreatedAt` gravado |
|---|---|---|---|
| 1 | 163d95 | text | `2026-08-20 15:15:18.524221` |
| 2 | ed4d86 | text | `2026-08-20 15:15:35.8191472` |
| 3 | 8e1a6f | text | `2026-08-20 15:19:21.4437036` |
| 4 | df6d8a | text | `2026-08-20 22:01:02.3681119` |

- Formato: texto `yyyy-MM-dd HH:mm:ss.fffffff`, sem `T`, sem `Z` e sem offset (`Kind` não é persistido). O primeiro valor tem 6 casas decimais porque o EF remove zeros à direita das ticks (100 ns).
- **Hipótese (não confirmada):** foram gravados por `DateTime.Now` na máquina de desenvolvimento, cujo fuso é `E. South America Standard Time` (UTC−03:00), ou seja, horário de Brasília. Indícios: a migration `AddShortUrlsTable` tem id `20260820181251` (o EF usa UTC: 18:12:51 UTC = 15:12:51 em Brasília), e o primeiro link foi criado às `15:15:18`. Se esse valor fosse UTC, o link teria sido criado 3 h antes da própria tabela existir (12:15 em Brasília). Isso sustenta a hipótese para os Ids 1–3; o Id 4 (`22:01`) não tem prova independente, apenas a mesma origem de código. O Brasil não tem horário de verão desde 2019, então o deslocamento seria sempre −03:00 nessas datas.
- **Regra proposta para a migração de dados (futura, sujeita à sua confirmação):**
  1. Cada valor antigo `L` (sem fuso) é interpretado como horário de `America/Sao_Paulo` e convertido: `UTC = L + 3 h` (usando `TimeZoneInfo`, não somando 3 h fixas, para ser correto também em datas com horário de verão no passado).
  2. Os valores resultantes seriam gravados em `timestamptz` com `Kind=Utc`, preservando as casas decimais (ex.: `2026-08-20 15:15:18.524221` → `2026-08-20 18:15:18.524221+00`).
  3. Corte: só registros criados antes da entrada em uso da versão UTC do código são convertidos. No banco atual são os 4 registros acima, todos de 2026-08-20; qualquer registro criado depois dessa mudança já é UTC e **não** deve ser convertido. Antes de migrar, confirmar o corte contra os dados no momento (por exemplo, por `Id` ou por data).
  4. Fazer a conversão apenas no script de carga para o PostgreSQL (nunca no SQLite de origem) e registrar os pares antes/depois para conferência.
  5. Se a hipótese for falsa (valores já em UTC), a regra deve ser descartada e os valores carregados como estão.
- Nenhuma conversão foi aplicada e nenhum registro foi alterado.
## Fluxo de desenvolvimento com PostgreSQL

| Comando | `HackerToolbox.Migrations.Postgres.dll` na saída do web? |
|---|---|
| `dotnet build Site` / `dotnet run --project Site` | **Sim**, desde que o projeto tenha sido restaurado pela solução (alvo `CopyPostgresMigrationsToOutput`: compila `Migrations.Postgres` com `BuildProjectReferences=false`, evitando ciclo, e copia o DLL para `bin/<Config>/net10.0/`) |
| `dotnet publish Site` | **Sim** (alvo `IncludePostgresMigrationsInPublish`) |
| Imagem Docker | Sim (restaura os dois projetos e usa `dotnet publish`) |

Requisito: `dotnet restore Site/HackerToolbox.slnx` (o restore apenas do `Site` não restaura `Migrations.Postgres`; nesse caso o build emite um aviso e o provider Postgres falha com mensagem clara, enquanto o SQLite não é afetado). Para desativar a cópia: `-p:BuildPostgresMigrations=false`.

A aplicação **nunca** aplica migrations no startup (não há chamada a `Migrate`); o schema é criado antes, por script ou `dotnet ef database update` explícito, apenas em banco de teste/ensaio:

```powershell
dotnet restore Site/HackerToolbox.slnx
$env:Database__Provider = "Postgres"
$env:ConnectionStrings__DefaultConnection = "Host=<host>;Database=<db>;Username=<user>;Password=<senha-de-teste>"
dotnet run --project Site
```
## Testes automatizados

Projeto `Tests/HackerToolbox.Tests` (MSTest, .NET 10; `global.json` ativa o runner Microsoft.Testing.Platform exigido pelo SDK 10). `ShortenerDateTests` chama o `ShortenerController` diretamente; `AdminSeedAndAuthTests` sobe o app real em memória (`WebApplicationFactory`, ambiente `Testing`, sem User Secrets) contra bancos isolados e cobre migrations, seed, login HTTP, `/Admin` e o encurtador:

```powershell
# SQLite (arquivo temporário) — os testes PostgreSQL ficam "ignorados" sem a variável
dotnet test --project Tests/HackerToolbox.Tests/HackerToolbox.Tests.csproj

# PostgreSQL descartável, sem portas publicadas: os testes rodam dentro de uma rede Docker
$d = "$env:TEMP\hbx-src"   # cópia do repositório sem .git, bin, obj, *.db
robocopy . $d /E /XD .git .vs .vscode bin obj Agent docs /XF *.db *.db-wal *.db-shm .env
$pw = [guid]::NewGuid().ToString('N'); docker network create hbx-pgtest-net
docker run -d --rm --name hbx-pg-scratch --network hbx-pgtest-net -e POSTGRES_PASSWORD=$pw -e POSTGRES_DB=scratch --tmpfs /var/lib/postgresql/data postgres:17
docker run --rm --network hbx-pgtest-net -v "${d}:/src" -w /src -e "HBX_TEST_PG_CONNECTION=Host=hbx-pg-scratch;Database=postgres;Username=postgres;Password=$pw" mcr.microsoft.com/dotnet/sdk:10.0 sh -c "dotnet restore Site/HackerToolbox.slnx && dotnet test --project Tests/HackerToolbox.Tests/HackerToolbox.Tests.csproj"
docker rm -f hbx-pg-scratch; docker network rm hbx-pgtest-net
```

Cada teste PostgreSQL cria e remove seu próprio banco (`hbx_test_<guid>`). Os testes aplicam as migrations em bancos temporários; nunca usam `Site/hackerbox.db`.

Limitação: a renderização completa das views no navegador e o scrubber de metadados (FFmpeg) não são cobertos por estes testes.

## Instalação PostgreSQL limpa (dados do SQLite descartados)

A base PostgreSQL nasce vazia: **nada é importado do SQLite**. O único registro criado é o administrador, pelo seed. As migrations nunca rodam na inicialização do site; aplique-as explicitamente.

```powershell
# 1. Banco vazio (no servidor PostgreSQL de destino; ajuste nomes e use uma senha forte)
psql -h <host> -U <admin_pg> -c "CREATE ROLE hbx LOGIN PASSWORD '<senha-forte>';"
psql -h <host> -U <admin_pg> -c "CREATE DATABASE hackerbox OWNER hbx;"

# 2. Aplicar as migrations (InitialPostgres) explicitamente
$env:HBX_DB_CONNECTION = "Host=<host>;Port=5432;Database=hackerbox;Username=hbx;Password=<senha-forte>"
dotnet ef database update --project Migrations.Postgres --startup-project Migrations.Postgres
# alternativa revisável: dotnet ef migrations script --idempotent ... -o initial.sql  e aplicar com psql -v ON_ERROR_STOP=1

# 3. Configurar o primeiro administrador e iniciar o site com o provider PostgreSQL
dotnet user-secrets set "AdminSeed:Email" "voce@exemplo.com" --project Site
dotnet user-secrets set "AdminSeed:Password" "<senha-inicial-forte>" --project Site
$env:Database__Provider = "Postgres"
$env:ConnectionStrings__DefaultConnection = $env:HBX_DB_CONNECTION
dotnet run --project Site
```

Em produção/Docker use as variáveis `AdminSeed__Email` e `AdminSeed__Password` (ou secrets), nunca arquivos versionados.

## Seed do administrador

A senha do seed pode vir de `AdminSeed:Password` ou, preferencialmente em contêiner, de `AdminSeed:PasswordFile` (Docker secret; não use os dois).

## Conexão PostgreSQL por componentes e secrets

Além da connection string completa, o app aceita `Database:Postgres:Host|Port|Database|Username|Password|PasswordFile` (variáveis `Database__Postgres__*`); a string é montada pelo `NpgsqlConnectionStringBuilder` (escaping correto de `;`, espaços, aspas). Em design-time (`dotnet ef` em `Migrations.Postgres`) use `HBX_PG_HOST|PORT|DATABASE|USERNAME|PASSWORD_FILE` (ou `HBX_DB_CONNECTION`). Ver [DOCKER.md](DOCKER.md) e `secrets/README.md`.
Implementado em `Site/Services/AdminSeeder.cs` (UserManager/RoleManager), executado na inicialização:

- Sem `AdminSeed:Email` e `AdminSeed:Password`: apenas um aviso; **nenhuma** conta ou role é criada (não há credencial padrão).
- Só um dos dois valores, e-mail inválido ou senha fora da política do Identity: a inicialização falha com mensagem clara, sem imprimir a senha.
- Cria a role `Admin` se faltar, cria o usuário só se não existir (`EmailConfirmed=true`) e atribui a role de forma idempotente.
- Usuário existente: **a senha nunca é redefinida**; só a role é garantida. Para trocar a senha use o fluxo normal do Identity.
- Falha em criar usuário/role/atribuição interrompe a inicialização. Com credenciais configuradas e o schema ainda não aplicado, o site não sobe: aplique as migrations antes.
- Depois de criado o administrador, as variáveis de seed podem ser removidas.

## Observações

- `Code` em `ShortUrls` não tem índice único (também no SQLite). Análise em cópia consistente somente leitura do banco real: 4 registros, 0 duplicatas (exatas ou sem diferenciar maiúsculas), 0 vazios. O código é gerado com `Guid` truncado em 6 caracteres, então colisões futuras são possíveis. Nenhum índice foi criado.
- A comparação de texto no PostgreSQL diferencia maiúsculas de minúsculas.
- Os tipos `TEXT`/`INTEGER` das migrations SQLite não são portáveis; por isso as migrations não são compartilhadas.
