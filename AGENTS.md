# AGENTS.md — Contexto Compartilhado do Projeto

> Este é o arquivo **fonte da verdade** para qualquer IA (Copilot, Claude, Gemini, GPT, etc.) que trabalhe neste repositório.
> Sempre que uma convenção, decisão de arquitetura ou regra de negócio mudar, atualize este arquivo — não duplique a informação em outro lugar.

## Visão Geral

**Hacker.Toolbox** — suite de utilitários web (dev tools, ferramentas de marketing, encurtador de URL) construída em ASP.NET Core MVC (.NET 9), com autenticação via ASP.NET Core Identity + RBAC.

## Stack

- **Runtime:** .NET 9.0, C#, `async/await` como padrão.
- **Web:** ASP.NET Core MVC (Razor Views, não Blazor/Razor Pages exceto Identity scaffolded em `Areas/Identity`).
- **Dados:** SQLite (`hackerbox.db`) via EF Core 9. Migrations em [Migrations/](Migrations).
- **Auth:** `AddDefaultIdentity<IdentityUser>` + `IdentityRole`, seed de usuário Admin automático no [Program.cs](Program.cs).
- **Front-end:** Tailwind CSS via CDN (paleta `#050505` dark / `#00ff66` terminal green), FontAwesome 6.4, jQuery + jQuery Validation.

## Estrutura de Pastas

- [Controllers/](Controllers) — um controller por área funcional (`DevToolsController`, `MktToolsController`, `ShortenerController`, `AdminController`, `ProxyReversoController`, `HomeController`).
- [Views/](Views) — espelha os controllers em subpastas (`DevTools/`, `MktTools/`, `Shortener/`, `Admin/`).
- [Models/](Models) — modelos de domínio simples (ex: `ShortUrl`).
- [Data/](Data) — `ApplicationDbContext` e `ApplicationDbContextFactory` (design-time).
- [Areas/Identity](Areas/Identity) — páginas de autenticação (Razor Pages scaffolded).

## Convenções

- Controllers e Views seguem o padrão MVC clássico do ASP.NET — evitar misturar com Razor Pages fora de `Areas/Identity`.
- Operações de I/O (arquivos, hashing, conversão de imagem) devem ser assíncronas.
- Ao adicionar tabelas/campos no EF Core, gerar migration nova (`dotnet ef migrations add <Nome>`), nunca editar migrations existentes já aplicadas.
- Strings de conexão não sensíveis ficam em `appsettings.Development.json`. Segredos reais (ex: senha do Admin seed) **nunca** vão no código nem em `appsettings*.json` — usar `dotnet user-secrets` em dev (`AdminSeed:Email` / `AdminSeed:Password`) e variáveis de ambiente em produção.
- Novas ferramentas/módulos seguem o padrão: Controller + View dedicados + entrada no menu lateral (`_Layout.cshtml`).

## Build / Run

```powershell
dotnet build
dotnet run
```

Banco local: `hackerbox.db` (SQLite), recriado/migrado automaticamente no startup via `Program.cs`.

## Regras para IAs de código (Copilot, Claude, Gemini, GPT, etc.)

1. Leia este arquivo antes de propor mudanças estruturais.
2. Depois de decisões relevantes (nova convenção, novo módulo, mudança de arquitetura), **atualize este arquivo** para manter as demais IAs sincronizadas.
3. Não crie arquivos de instrução paralelos com informações divergentes — se precisar de instruções específicas de uma ferramenta, referencie este arquivo em vez de repetir o conteúdo.
