# Rubinot Tunnel Agent

Agente desktop (WinForms, .NET 8) que roda na máquina do usuário e encaminha
o tráfego do client do jogo para o seu relay TCP (o `DevToolsController` /
`TuneladorTcp` que já está no seu backend).

## Como funciona

```
Client do jogo  --->  127.0.0.1:PortaLocal (este agente)
                                 |
                                 v
                     RelayHost:RelayPort (seu servidor, StartTunnel)
                                 |
                                 v
                          Rubinot (destino final)
```

O usuário só precisa:
1. Abrir o `RubinotTunnelAgent.exe`.
2. Preencher o **endereço do relay** (o host público onde seu `StartTunnel`
   está escutando) e a **porta do relay**.
3. Clicar em "Iniciar Túnel".
4. Configurar o client do jogo para conectar em `127.0.0.1:PortaLocal`
   (por padrão, a mesma porta que o servidor real usa).

As configurações ficam salvas em
`%LocalAppData%\RubinotTunnelAgent\config.json`, então na próxima abertura o
usuário não precisa preencher tudo de novo — e com "Iniciar automaticamente"
marcado, o túnel já sobe sozinho.

## Build local (desenvolvimento)

Requer .NET 8 SDK com o workload de Windows Desktop (só builda em Windows,
ou em Linux/Mac com o SDK cross-compilando pra `net8.0-windows`, mas rodar
o `.exe` final exige Windows por causa do WinForms).

```bash
dotnet build
dotnet run
```

## Gerar o .exe para distribuir aos usuários

Isso gera um único `.exe` self-contained — o usuário não precisa ter o
.NET instalado:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

O executável final fica em:
`bin/Release/net8.0-windows/win-x64/publish/RubinotTunnelAgent.exe`

## Ajustes recomendados antes de distribuir

- **Ícone**: adicione um `app.ico` na raiz do projeto e descomente o bloco
  `<ApplicationIcon>` no `.csproj`.
- **Autenticação no relay**: hoje o `StartTunnel` do `DevToolsController`
  aceita qualquer conexão na porta local. Se for expor publicamente, vale
  adicionar um token simples (ex: header customizado ou handshake inicial)
  pra evitar que terceiros usem seu relay pra abrir túnel pra qualquer
  destino, não só o Rubinot. Posso ajudar a implementar isso dos dois
  lados (agente + controller) se quiser.
- **Assinatura de código**: como é um `.exe` publicado sem assinatura
  digital, o Windows SmartScreen/Defender pode alertar o usuário na
  primeira execução. Se isso incomodar, considere um certificado de
  assinatura de código (Authenticode).
- **Auto-update**: se for iterar bastante, vale um mecanismo simples de
  checar versão contra um endpoint do seu backend e avisar o usuário pra
  baixar a nova versão.

## Estrutura de arquivos

- `TcpForwarder.cs` — motor de encaminhamento TCP local → relay.
- `MainForm.cs` — interface (configuração, status, log, bandeja do sistema).
- `Program.cs` — bootstrap da aplicação.
- `RubinotTunnelAgent.csproj` — projeto .NET 8 WinForms.
