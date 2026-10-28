# Docker — NewHackerTools (execução local)

Infraestrutura: `Dockerfile`, `.dockerignore`, `compose.yaml`, `.env.example` (raiz do repositório).
Este guia cobre **execução local**. Deploy no VPS ainda não foi feito.

## Arquitetura

- Um único serviço `web` (ASP.NET Core 10, Ubuntu 24.04), **uma instância apenas** — SQLite não deve ter múltiplos processos escritores.
- Executa como `app` (UID 1654), `cap_drop: ALL`, `no-new-privileges`, escuta em `8080` dentro do container.
- Porta publicada somente em `127.0.0.1:${HBX_HOST_PORT:-8080}`. Nenhuma outra porta é exposta.
- Volumes nomeados (modo `0700`, dono `app`):

| Volume | Caminho | Conteúdo |
|---|---|---|
| `hackertools_hbx-data` | `/data` | SQLite (`/data/hackerbox.db`) |
| `hackertools_hbx-keys` | `/keys` | Chaves de ASP.NET Core Data Protection |

- O banco **não** é copiado para a imagem (`.dockerignore` exclui `*.db*`) e **migrations não são aplicadas automaticamente**.
- ffmpeg/ffprobe vêm do pacote Ubuntu (`/usr/bin`). Arquivos temporários do scrubber (`Path.GetTempPath()` → `/tmp`) ficam no disco do container, são descartados ao recriá-lo e continuam sendo limpos pela retenção de 2 h do código (a limpeza só roda a cada novo upload).
- `GET /healthz` (anônimo, sem acesso a banco ou segredos) alimenta o `HEALTHCHECK`.

## Configuração

```powershell
Copy-Item .env.example .env   # .env é ignorado pelo Git; preencha localmente
```

Variáveis principais: `HBX_HOST_PORT`, `ADMIN_SEED_EMAIL`/`ADMIN_SEED_PASSWORD`, `HBX_TRUSTED_PROXIES`, `HBX_MEM_LIMIT`, `HBX_CPUS`.

## Comandos

```powershell
docker compose config            # valida/expande a configuração
docker compose build             # build da imagem
docker compose up -d             # inicia
docker compose ps                # estado e health
docker compose logs -f web       # logs (stdout/stderr)
docker compose stop              # para sem remover volumes
docker compose down              # remove container/rede; volumes permanecem
# docker compose down -v         # CUIDADO: apaga os volumes (banco e chaves)
```

Diagnóstico:

```powershell
curl http://127.0.0.1:8080/healthz
docker compose exec web sh -c "id; ffmpeg -version | head -1; ffprobe -version | head -1; ls -la /data /keys"
docker inspect --format '{{json .State.Health}}' hackertools-web-1
docker stats --no-stream
```

## Banco PostgreSQL com Compose (overlay `compose.postgres.yaml`)

O `compose.yaml` sozinho continua em SQLite. O overlay adiciona `db` (PostgreSQL 17, volume próprio `hbx-pgdata`, sem porta publicada, healthcheck `pg_isready`), troca o provider do `web` para PostgreSQL e define o serviço `migrate` (perfil `tools`, nunca sobe com `up`). O volume SQLite (`hbx-data`) não é usado pelo PostgreSQL.

```powershell
# 0. Secrets (não versionados; veja secrets/README.md para criar com permissões restritas) e .env (só nomes não sensíveis)
Copy-Item .env.example .env
[IO.File]::WriteAllText("$PWD\secrets\hbx_pg_password.txt", "<senha-forte-do-postgres>")
[IO.File]::WriteAllText("$PWD\secrets\admin_seed_password.txt", "<senha-inicial-do-admin>")   # vazio = sem admin
# preencha ADMIN_SEED_EMAIL no .env
$c = @('-f','compose.yaml','-f','compose.postgres.yaml')

docker compose @c config --quiet                       # valida (use --quiet: "config" sem isso imprime os valores do .env)
docker compose @c build web
docker compose @c up -d --wait db                      # 1-2. inicia o PostgreSQL e espera o healthcheck
docker compose @c --profile tools run --rm migrate     # 3. aplica InitialPostgres e futuras migrations (explícito)
docker compose @c up -d --wait web                     # 4-5. inicia o site; o seed cria o admin (se ADMIN_SEED_* definidos)
docker compose @c logs -f web
```

Notas:
- **Ordem obrigatória:** com `ADMIN_SEED_*` definidos e schema ausente, o `web` falha ao iniciar (por projeto). Rode `migrate` antes. O site nunca aplica migrations.
- Futuras migrations: gere em `Migrations.Postgres`, reconstrua (`build migrate web`), **faça backup** (seção abaixo), pare o `web`, rode `migrate`, suba o `web`.
- Depois do primeiro administrador criado, esvazie `ADMIN_SEED_EMAIL` no `.env` e o arquivo `secrets/admin_seed_password.txt`.
- **Credenciais:** as senhas vão em Compose secrets (arquivos em `secrets/`, montados em `/run/secrets`), não em variáveis de ambiente: não aparecem em `docker inspect`, `env` do container nem nos logs (verificado). A connection string é montada pelo Npgsql a partir de componentes (`Database__Postgres__*`), então senhas com espaços, `;`, aspas etc. funcionam (testado). Secrets **não** protegem contra quem tem acesso privilegiado ao Docker ou ao arquivo no host. `docker compose config` sem `--quiet` imprime valores do `.env`: não cole a saída em logs/issues.
- **Privilégios:** `migrate` roda como uid 1654, com `cap_drop: ALL` e `no-new-privileges`. Não usa `read_only` porque o `dotnet-ef` grava artefatos do MSBuild em `/src/**/bin`. O estágio `migrate` contém o SDK e o código-fonte (ferramenta efêmera; não é a imagem do site). O `web` mantém as proteções originais.
- Rollback para SQLite: `docker compose up -d` usando apenas `compose.yaml` (provider padrão SQLite, volume `hbx-data` intacto; o arquivo original `Site/hackerbox.db` nunca é tocado). O volume `hbx-pgdata` permanece até você removê-lo explicitamente.
- Não use `down -v` no ambiente real: apaga `hbx-pgdata`.
- Mantenha uma única instância `web`.

### Backup e restauração (formato custom do `pg_dump`)

O dump não contém senhas de roles (verificado), mas contém dados dos usuários (hashes de senha do Identity, links): trate `backups/` (ignorado pelo Git e pelo `.dockerignore`) como sensível. O `pg_dump` roda dentro do container `db` pelo socket local (sem senha em linha de comando ou variável).

```powershell
$c = @('-f','compose.yaml','-f','compose.postgres.yaml')
New-Item -ItemType Directory backups -Force | Out-Null
$f = "hbx-$(Get-Date -Format yyyyMMdd-HHmmss).dump"

# Backup (consistente com o site em execução)
docker compose @c exec -T db sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc --no-owner --no-acl -f /tmp/hbx.dump && pg_restore --list /tmp/hbx.dump > /dev/null'
docker compose @c cp db:/tmp/hbx.dump "backups/$f"
docker compose @c exec -T db rm /tmp/hbx.dump
# NÃO use "pg_dump > arquivo" no PowerShell: o redirecionamento corrompe o formato binário.

# Restauração em uma base NOVA e vazia (nunca por cima da base em uso)
docker compose @c cp "backups/$f" db:/tmp/restore.dump
docker compose @c exec -T db sh -c 'createdb -U "$POSTGRES_USER" hbx_restored && pg_restore -U "$POSTGRES_USER" -d hbx_restored --exit-on-error --no-owner --no-acl /tmp/restore.dump'
docker compose @c exec -T db rm /tmp/restore.dump

# Verificar (tabelas, usuários, roles, links, histórico de migrations)
docker compose @c exec -T db psql -U hbx -d hbx_restored -c 'select (select count(*) from "AspNetUsers") users, (select count(*) from "AspNetRoles") roles, (select count(*) from "ShortUrls") links, (select count(*) from "__EFMigrationsHistory") migrations'

# Usar a base restaurada: pare o web e suba-o apontando para ela (HBX_PG_DB no .env ou no shell)
$env:HBX_PG_DB = 'hbx_restored'; docker compose @c up -d --wait web; Remove-Item Env:HBX_PG_DB
```

Para uma restauração definitiva, torne `HBX_PG_DB=hbx_restored` permanente no `.env` (o `POSTGRES_DB` só vale na criação do volume; o healthcheck do `db` usa essa variável e a base precisa existir) ou renomeie bancos com `ALTER DATABASE` após parar o `web`. Mantenha ao menos um backup fora do host/volume Docker e teste a restauração periodicamente. Para perda total do volume: suba só o `db`, crie a base e restaure o dump (o dump já traz schema e histórico de migrations; não rode `migrate` antes).

Validado em ambiente descartável: dump → restauração em outra base → mesmos dados (hash md5 idêntico de `AspNetUsers` e `ShortUrls`, mesmas 9 tabelas e histórico) → login, `/Admin` e redirecionamento de link funcionando pelo site apontado para a base restaurada, inclusive após recriar os containers.
- Validado com projeto descartável (`-p hbxtest`, `--env-file` temporário, `down -v` ao final): config, build, healthcheck do db, migrate, startup, seed, login/Admin/encurtador, recriação dos containers com dados preservados.

### Instalação limpa e testes em recursos descartáveis

Para ensaiar o procedimento sem tocar nos recursos reais, copie o repositório (sem `.git`, `bin`, `obj`, `*.db`, `.env` e secrets) para outra pasta e, **antes** do passo 0, defina um nome de projeto e uma porta exclusivos, senão os volumes/portas do projeto real (`hackertools`, porta 8080) seriam reutilizados:

```powershell
$env:COMPOSE_PROJECT_NAME = 'hbxclean'      # volumes/rede/containers próprios (compose.yaml fixa "name: hackertools" apenas como padrão)
# no .env da cópia: HBX_HOST_PORT=18091 e ADMIN_SEED_EMAIL=<email>
# ao final: docker compose @c --profile tools down -v   (somente na cópia descartável)
```

A imagem usa a tag fixa `hackertools-web:local`; construir na cópia reutiliza/atualiza essa tag (mesmo conteúdo, não afeta volumes). Passos que exigem ação manual além dos comandos acima: preencher `ADMIN_SEED_EMAIL` no `.env` (o `.env.example` o deixa vazio) e ajustar projeto/porta quando houver outra instalação.

Validado (instalação limpa): secrets → `up -d --wait db` → `run --rm migrate` → `up -d --wait web` → login (senha errada = 200 com erro, correta = 302), `/Admin` = 200 (anônimo = 302), criação e redirecionamento de URL curta → backup → restauração em `hbx_restored` → site apontado para a base restaurada (login, `/Admin` e redirecionamento funcionando; 1 usuário, 1 role, 1 vínculo usuário-role, 1 link, 1 migration).

### Secrets em Linux (permissões reais)

No Docker Desktop/Windows os secrets de arquivo aparecem em `/run/secrets` como `root:root 777`, o que não representa um host Linux. Em Linux o arquivo do host é montado com o dono/modo originais; o usuário `app` (uid 1654) do `web` e do `migrate` precisa lê-lo. Reproduzido com volume nomeado (`volume-subpath`), arquivos `0444` e raiz `0700`: `db`, `migrate` (uid 1654, `cap_drop ALL`) e `web` iniciam, aplicam `InitialPostgres` e criam o admin; o uid 1654 lê o secret; senhas com espaço, `;` e `"` funcionam; nenhuma senha em `env`, `docker inspect` ou logs. Com o arquivo `0600 root:root` o `migrate` falha com `Access to the path '/run/secrets/...' is denied`: por isso `chmod 444` nos arquivos dentro da pasta `700`. Equivalente ao Compose, não o Compose literal em um host Linux.

### Scrubber de metadados (validado com arquivos sintéticos)

Fluxo `POST /MktTools/ScrubFile` (login + token antiforgery) testado via HTTP no container (ffmpeg/ffprobe 6.1.1 presentes) e na suíte (`MetadataScrubberHttpTests`, SQLite e PostgreSQL):
- JPEG (EXIF/IPTC/COM removidos, JFIF e dados mantidos), PNG (`tEXt`/`tIME` removidos), DOCX (`core.xml`/`app.xml` esvaziados, corpo mantido) e MP4 (title/comment/encoder removidos pelo ffmpeg; restam apenas `major_brand`/`minor_version`/`compatible_brands`/handler do stream, que não identificam o autor).
- PDF: copiado sem alteração, `supported=false`. Vazio: 400 "Arquivo vazio."; lote inválido: 400; arquivo corrompido: 500 `{error}` sem deixar `.out`/`.name`; arquivo de 500 MB + 1 byte: 400 "Arquivo excede 500 MB.".
- Temporários em `/tmp/hbx_scrub/<usuário>/<lote>`: `.in` sempre apagado; `.out`/`.name` ficam para download; lotes com mais de 2 h são removidos na próxima chamada de `ScrubFile` (testado envelhecendo o diretório). Outro usuário não baixa o resultado (404); sem login, 302.
- Limitações: o limite de 500 MB só foi exercitado manualmente (não está na suíte); a mensagem 500 de falha do ffmpeg inclui o stderr dele, que revela o caminho temporário (`/tmp/hbx_scrub/<id do usuário>/...`) ao próprio usuário autenticado (não alterado); os temporários só são limpos quando alguém chama `ScrubFile` (sem job periódico) e ficam na camada gravável do container.

## Banco SQLite (procedimento manual — não executado automaticamente)

Num volume novo o arquivo `hackerbox.db` nasce **vazio, sem schema**: a aplicação sobe e o `/healthz` responde, mas login e demais telas que usam o banco falham (o seed do Admin registra erro no log). O schema deve ser levado ao volume de forma explícita e deliberada:

**Opção A — copiar um backup consistente do banco atual** (não altera o original; o banco local usa WAL, então não copie o arquivo "a quente"):

```powershell
docker compose stop web
New-Item -ItemType Directory -Force backup | Out-Null
sqlite3 Site\hackerbox.db ".backup backup\hackerbox.db"      # requer sqlite3 no host
docker run --rm --user 1654:1654 -v hackertools_hbx-data:/data -v "${PWD}\backup:/src:ro" ubuntu:24.04 cp /src/hackerbox.db /data/hackerbox.db
docker compose up -d
```

**Opção B — banco novo**: gerar um script SQL com `dotnet ef migrations script --idempotent` (apenas gera texto) e aplicá-lo ao arquivo do volume com `sqlite3`, após revisão.

Backup do volume: pare o `web` e copie `/data` (ou use `sqlite3 .backup`). Faça o mesmo com `/keys`; sem as chaves, sessões/cookies existentes são invalidados.

## Logs e rotação

Driver `json-file` com `max-size: 10m` e `max-file: 5` por container (ajustável em `compose.yaml`). Para aplicar o padrão a todo o host, configure `/etc/docker/daemon.json` (`log-driver`/`log-opts`) ou use `journald`.

## Proxy HTTPS externo (futuro)

Não há proxy dentro do projeto. O proxy HTTPS do VPS (Nginx/Caddy, no host) deve encaminhar para `127.0.0.1:8080`, **sobrescrever** `X-Forwarded-For`/`X-Forwarded-Proto` e ter limites compatíveis com uploads de 500 MB (ex.: `client_max_body_size`, timeouts).

A aplicação só confia em forwarded headers de `HBX_TRUSTED_PROXIES` (IPs/CIDRs separados por vírgula; vazio = desativado). A rede do Compose usa sub-rede fixa `172.28.0.0/24`; um proxy no host chega ao container como `172.28.0.1`. Se essa sub-rede conflitar com outra rede sua, altere-a em `compose.yaml` e o valor de `HBX_TRUSTED_PROXIES`. Em produção atrás de TLS, adicione `HTTPS_PORT: "443"` ao `environment` do serviço para ativar o redirecionamento HTTPS (não testado; `/healthz` é isento do redirecionamento).

## Limites de recursos

`mem_limit` (`HBX_MEM_LIMIT`, padrão `2g`) e `cpus` (`HBX_CPUS`, padrão `2`) funcionam com `docker compose up` sem Swarm. A conversão de imagens e o ZIP ocorrem parcialmente em memória e o ffmpeg consome CPU; ajuste após medir com `docker stats`. Cada arquivo de até 500 MB ocupa entrada + saída em `/tmp` durante a retenção; reserve disco compatível. Os limites de upload do código não foram alterados.

## Observações

- A imagem é grande (~1 GB) por causa das dependências do ffmpeg do Ubuntu.
- Não aumente réplicas do `web` enquanto usar SQLite.
