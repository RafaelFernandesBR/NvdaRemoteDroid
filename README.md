# NvdaRemoteDroid

App .NET MAUI (Android) que controla remotamente o NVDA usando o protocolo NVDA Remote, atuando como papel **master/leader** (controlador). Suporta conexão direta a um relay self-hosted (IP:porta) e conexão ao relay público (`nvdaremote.com:6837`) via chave de canal.

## Estrutura

- `NvdaRemoteDroid.Core` — protocolo, teclado, fala, state machine de conexão, ViewModels base. `net10.0` puro, testável sem emulador.
- `NvdaRemoteDroid` — app MAUI (`net10.0-android`): Pages, transporte TCP/TLS real, TTS nativo, serviços de preferências/clipboard/certificados.
- `NvdaRemoteDroid.Tests.Unit` — xUnit + NSubstitute + Shouldly, referencia só o Core.

## Compilar e testar

```
dotnet build NvdaRemoteDroid.slnx
dotnet test NvdaRemoteDroid.Tests.Unit/NvdaRemoteDroid.Tests.Unit.csproj
```

Requer o workload `maui-android` instalado (`dotnet workload install maui-android`).

## Escopo da v1

Dentro: handshake completo, envio de teclas (teclado virtual na tela + físico/Bluetooth), Ctrl+Alt+Del, clipboard, recebimento e reprodução de fala (TTS nativo Android), motd, tone (feedback por vibração), reconexão automática com backoff de 5s, TLS com TOFU por fingerprint em modo inseguro.

Fora de escopo (documentado no plano original): papel slave/follower, `generate_key` (só existe no relay público), braille físico, `wave` (áudio do remetente), layout de teclado "laptop", pausa real de fila no TTS.

## Simplificações conhecidas da v1

- A chave do canal é persistida em `Preferences` (armazenamento privado do app, não criptografado) em vez de `SecureStorage` — razoável por ser uma senha de sala, não uma credencial de conta.
- `TcpTlsTransportService.HandleLine` (o roteamento de mensagens recebidas para transições de estado) não tem teste automatizado direto — o parser e a state machine puros têm cobertura completa, mas a integração entre eles só foi verificada manualmente/por revisão de código.
