readme_content = """<div align="center">

# ⚡ HACKER.TOOLBOX ⚡
### *Advanced Core Utilities & Operational Suite*

<p align="center">
  <img src="https://img.shields.io/badge/RUNTIME-.NET%209.0-00ff66?style=flat-square&logo=dotnet&logoColor=black" alt=".NET 9">
  <img src="https://img.shields.io/badge/FRAMEWORK-ASP.NET%20CORE%20MVC-050505?style=flat-square&logo=csharp&logoColor=00ff66" alt="ASP.NET Core">
  <img src="https://img.shields.io/badge/DATABASE-SQLite%20%2F%20EF%20Core-262626?style=flat-square&logo=sqlite&logoColor=00ff66" alt="SQLite">
  <img src="https://img.shields.io/badge/SECURITY-ASP.NET%20Identity%20%2B%20RBAC-00ff66?style=flat-square&logo=security&logoColor=black" alt="Security">
  <img src="https://img.shields.io/badge/LICENSE-MIT-red?style=flat-square" alt="License">
</p>

</div>

---

> *Ambiente de operações e utilitários de alta performance desenvolvido para engenheiros de software, pentesters e analistas de sistemas. Focado em resiliência, concorrência assíncrona e isolamento de fluxos sob uma estética imersiva de terminal.*

---

## 🚀 VISÃO GERAL DO SISTEMA

O **Hacker.Toolbox** foi construído do zero utilizando as diretrizes mais avançadas do ecossistema moderno da Microsoft (.NET 9). A plataforma opera sob um núcleo altamente restrito e seguro, integrando ferramentas essenciais de desenvolvimento, inspeção criptográfica, processamento multimídia em lote e gestão de tráfego.

### 🛡️ Módulos Principais Ativos (`// SYSTEM_MODULES`)

1. **[DEV_TOOLS]**
   - **JSON & JWT Inspector:** Validação rigorosa de sintaxe estrutural de JSON (com captura exata de exceções de compilação) e decodificador forense de tokens JWT (separando *Header*, *Payload* e *Signature* ao estilo de inspetores avançados).
   - **Hashes & Cripto:** Motor assíncrono para geração em tempo real de impressões digitais criptográficas e codificações (`Base64`, `MD5`, `SHA-256`, `SHA-512`).

2. **[MKT_TOOLS]**
   - **Mass Image Converter & Renamer:** Conversor de lote de alta performance com suporte a 7 formatos web/corporativos (`JPEG`, `PNG`, `WebP`, `BMP`, `GIF`, `TIFF`, `ICO`), regras de renomeação sequencial dinâmica e empacotamento automático em `.zip`.
   - **Metadata Scrubber (Exif Cleaner):** Higienização forense de arquivos (imagens, PDFs e documentos corporativos) para remoção de coordenadas GPS, dados EXIF e metadados de autoria.
   - **Cyber Link Shortener:** Encurtador de URLs próprio acoplado ao banco de dados para rastreamento de cliques em tempo real e redirecionamentos otimizados em servidores de produção.

3. **[SYSTEM_CORE & SECURITY]**
   - **Autenticação Baseada em Roles (RBAC):** Controle de acesso rigoroso gerenciado pelo ASP.NET Core Identity com suporte a painel administrativo para gestão de operadores.
   - **Layout Adaptativo:** Interface responsiva com header fixo, indicador de status de link seguro (`SECURE_LINK`) e menu lateral esquerdo retrátil com colapso inteligente de ícones.

---

## 🛠️ STACK TECNOLÓGICA

- **Runtime:** .NET 9.0 (C# moderno, orientação estrita a concorrência assíncrona `async/await`).
- **Arquitetura Web:** ASP.NET Core MVC com Views estruturadas em Razor.
- **Persistência:** SQLite (`hackerbox.db`) via Entity Framework Core 9 (com suporte a Design-Time Factory).
- **Interface & Estilização:** Tailwind CSS (via CDN) com paleta personalizada (`#050505` dark, `#00ff66` terminal green) e ícones via FontAwesome 6.4.

---

## ⚙️ GUIA DE INSTALAÇÃO E EXECUÇÃO

Certifique-se de ter o **.NET 9 SDK** instalado na sua máquina antes de prosseguir.