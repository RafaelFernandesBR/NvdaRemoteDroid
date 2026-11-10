# Arquitetura — camadas e fluxo de dados

Detalhe das camadas citadas no `CLAUDE.md`. Leia aquele arquivo primeiro para o mapa geral; este documento é o aprofundamento de cada peça.

## Protocolo NVDA Remote

A engenharia reversa original do protocolo foi feita num relatório fora deste repositório (investigação do código-fonte do NVDA); o resumo abaixo é o que de fato foi implementado em `NvdaRemoteDroid.Core/Protocol/` — esta seção é a fonte da verdade sobre o que o app faz, não precisa do relatório original para dar manutenção aqui.

- **Transporte**: TCP + TLS 1.2. O app nunca fala direto com o NVDA — sempre com um relay (self-hosted pelo próprio NVDA, ou o público `nvdaremote.com:6837`).
- **Framing**: uma mensagem JSON UTF-8 por linha, terminada em `\n`, sem prefixo de tamanho. `LineFramer` implementa isso sobre `Stream` puro (testável com `MemoryStream`/stream fake de chunks pequenos), cuidando de caracteres UTF-8 multibyte partidos entre leituras.
- **Handshake**: `ProtocolVersionMessage(version: 2)` imediatamente após o TLS concluir, depois `JoinMessage(channel, "master")`. Sucesso → `ChannelJoinedMessage`. Senha errada → `ErrorMessage` seguido do relay fechando o socket. Sem resposta nenhuma em 15s → o transporte derruba o socket e cai no fluxo normal de reconexão.
- **Mensagens enviadas pelo master**: `KeyMessage`, `SendSasMessage`, `SetClipboardTextMessage`.
- **Mensagens recebidas e repassadas via `ITransportService.MessageReceived`** (sempre na UI thread, na mesma ordem em que chegaram): `ChannelJoinedMessage`, `ClientJoinedMessage`, `ClientLeftMessage`, `SpeakMessage`, `CancelMessage`, `PauseSpeechMessage`, `ToneMessage`, `SetClipboardTextMessage`, `MotdMessage`, `ErrorMessage` — todas tratadas pelo `RemoteSessionCoordinator` (ver seção Sessão), não por ViewModels. `PingMessage` é ignorada de propósito, nunca respondida. `VersionMismatchMessage` só dispara transição de estado. `UnknownMessage` é logada (tipo, tamanho e **formato**, nunca valores — `RemoteMessageShape`) e descartada.
- **Fora de escopo v1**: papel slave/follower, `generate_key` (só existe no relay público), braille (`display`/`braille_input`/`set_braille_info`), `wave` (caminho de arquivo do remetente, inútil cross-plataforma).

### Parser tolerante (`RemoteMessageParser`)

Não usa `[JsonPolymorphic]` — lê a propriedade `"type"` via `JsonDocument` e despacha por `switch`. As mensagens de canal (`channel_joined`, `client_joined`, `client_left`) são lidas **à mão, campo a campo**: só o `type` é obrigatório, `channel`/`user_ids`/`clients` podem faltar ou vir em outro formato (ids como inteiro, decimal ou string) sem invalidar a mensagem. Isso é deliberado — um `channel_joined` rejeitado deixa a conexão presa em `Handshaking` e o teclado inteiro desabilitado (bug real, visto em log de campo com o relay público). As demais usam `JsonSerializer.Deserialize<TConcreto>`. Motivos do desenho:

1. O protocolo tolera campos extras desconhecidos (o NVDA real também tolera), então o parser nunca deve lançar por causa deles.
2. Um tipo de mensagem desconhecido (`index`, `nvda_not_connected`, `generate_key`) ou uma mensagem com campo obrigatório ausente (ex. `speak` sem `sequence`) vira `UnknownMessage(Type, RawJson)` — logado e descartado, nunca propaga exceção para quem chama `TryParse`.

### `speak.sequence` heterogêneo

`SpeechSequenceJsonConverter` lê cada item do array: uma string pura vira `SpeechTextItem`; um array `[ClassName, {attrs}]` vira `SpeechCommandItem` (classe desconhecida é ignorada com segurança, sem quebrar o parse da mensagem toda). `SpeechSequenceTextBuilder.ToUtterances` reduz isso a texto simples para o TTS: concatena os trechos de texto e trata `EndUtteranceCommand` como quebra de frase; qualquer outro comando (`LangChangeCommand`, `BreakCommand`, etc.) é ignorado — a v1 não tenta reproduzir prosódia.

## Transporte (`TcpTlsTransportService`)

Vive no projeto Android (`NvdaRemoteDroid/Services/TcpTlsTransportService.cs`), não no Core, porque usa `System.Net.Sockets`/`System.Net.Security` diretamente — não há nada Android-específico nele, só não foi extraído para o Core por não precisar ser testado com mocks de rede.

- `TcpClient { NoDelay = true }` + `SslStream` com `TargetHost` sempre setado e `SslProtocols.Tls12`.
- **TOFU (modo inseguro)**: `RemoteCertificateValidationCallback` aceita certificados válidos normalmente; em modo inseguro, calcula o fingerprint SHA-256 do certificado DER e compara contra `ICertificateTrustStore` (persistido via `Preferences`, chave `host:porta`). Se desconhecido, a validação retorna `false` — o que faz `AuthenticateAsClientAsync` **lançar** `AuthenticationException` (não retornar `false` silenciosamente). O catch específico para esse caso dispara o evento `CertificateTrustRequested`, aguarda a decisão do usuário via `TaskCompletionSource`, e — se aceito — refaz a conexão do zero recursivamente (o `SslStream` abortado é inutilizável).
- **Fila de envio**: um `Channel<string>` consumido por uma única task (`RunSendLoopAsync`) — evita que escritas concorrentes corrompam o stream. `SendAsync` usa `TryWrite`, só aceita mensagens com o estado `Authenticated` (o handshake entra direto na fila) e nunca lança por causa do estado da conexão: quem chama são handlers de UI e o `DispatchKeyEvent`.
- **Ciclo de vida**: `ConnectAsync`/`DisconnectAsync` são serializados por um `SemaphoreSlim` e sempre encerram o laço anterior antes de abrir outro. `StateChanged` só dispara quando o estado realmente muda.
- **Keep-alive TCP** ligado em todo socket (60s ocioso, sondas a cada 15s) para perceber conexões mortas atrás de NAT.
- **Leitura**: um `LineFramer` **novo por tentativa de conexão** (nunca reaproveitado entre reconexões — ver gotcha no `CLAUDE.md`) alimenta `RemoteMessageParser.TryParse` → `HandleLine`, que decide transições de estado e repassa a mensagem via o evento `MessageReceived`.
- **Reconexão**: backoff fixo de 5s, só a partir de `ConnectionState.Error` (falha de IO/transporte). Um EOF "limpo" (o relay fecha o socket sem exceção) é tratado como `TransportFailed` em qualquer estado — a state machine mantém `AuthFailed`/`VersionMismatch` terminais e leva `Handshaking`/`Authenticated` para `Error`. Ao sair do laço de leitura a fila de envio é completada **antes** de aguardar o laço de envio (senão o `await` nunca termina e a reconexão nunca acontece).

## State machine de conexão (`RemoteConnectionStateMachine`)

Pura, sem nenhuma dependência de rede — só `Apply(ConnectionEvent)` sobre um `enum ConnectionState`:

```
Disconnected → Connecting → { AwaitingCertificateTrust | Handshaking | Error }
Handshaking  → { Authenticated | AuthFailed | VersionMismatch | Error }
Authenticated → { Error | Disconnected }
```

`AuthFailed` e `VersionMismatch` são terminais: um evento `TransportFailed` subsequente não os move para `Error` (ver tabela de transições em `RemoteConnectionStateMachineTests`). Isso existe para não martelar o relay com reconexões automáticas quando o usuário digitou a senha errada.

## Teclado

Dois caminhos de entrada, ambos produzindo `KeyMessage` via `KeyEventFactory` (que garante o par down/up e `scan_code: null`):

1. **Teclado virtual**: a tabela de teclas é `VirtualKeyboardLayout` (Core, pura e testada) — seis grupos (`Atalhos`, `Navegação`, `Letras`, `Números e símbolos`, `Funções`, `Teclado numérico`), cada tecla com rótulo, nome acessível localizado, `VirtualKeyMapping` e modificadores próprios (é o que faz um atalho como NVDA+T). `NvdaRemoteDroid/Controls/VirtualKeyboardView.cs` só desenha: modificadores no topo, seletor de grupo e as teclas de **um grupo por vez**, tudo em `Grid` de colunas iguais calculadas pela largura (nada de `FlexLayout`/rolagem horizontal — era isso que cortava as teclas para fora da tela). Modificadores Ctrl/Alt/Shift/Win/NVDA têm três estados: desligado → vale para a próxima tecla → travado. O conjunto de modificadores ativos guarda `VirtualKeyMapping` completo (não só o `VkCode`) para não perder o bit `Extended` da tecla Windows. O campo "Digitar texto" da tela envia `set_clipboard_text` + Ctrl+V (independe do layout de teclado do PC e cobre acentos).
2. **Teclado físico/Bluetooth**: `MainActivity.DispatchKeyEvent` intercepta `KeyEvent` só enquanto `Shell.Current.CurrentPage` é `RemoteControlPage`, a sessão está `Authenticated` e o foco **não** está num `EditText` (o campo "Digitar texto" fica nessa página) — nos demais casos o evento segue o fluxo normal do Android. Mapeamento via `AndroidKeyToVirtualKeyMap` (tabela pura, testável, `Dictionary<AndroidKeyCode, VirtualKeyMapping>`).

## Sessão (`RemoteSessionCoordinator`)

`NvdaRemoteDroid.Core/Session/RemoteSessionCoordinator.cs` é singleton e é instanciado junto com o `App` (injetado no construtor). Ele assina `MessageReceived`/`StateChanged` do transporte e é o único lugar que trata o que chega do NVDA: `speak`/`cancel`/`pause_speech` → `ISpeechOutputService`, `set_clipboard_text` → `IClipboardService`, `motd` (com dedupe) e `tone` → `ISessionFeedback` (implementado por `SessionFeedbackService` no projeto Android). Também acompanha a **presença** do computador controlado no canal (`channel_joined.clients` + `client_joined`/`client_left`), expõe `IsRemoteComputerPresent`/`PresenceChanged` e anuncia entrada/saída. Isso não pode morar numa ViewModel: elas são transient e só existem depois que a aba é aberta, então a fala só começaria após visitar a tela de Controle e não funcionaria em segundo plano.

## Serviço em primeiro plano (Android)

`Platforms/Android/RemoteConnectionForegroundService.cs` (tipo `specialUse`, declarado à mão no `AndroidManifest.xml` por causa do `<property>` de subtipo; a classe usa `[Register]` com nome fixo, sem `[Service]`) mantém o processo vivo com o app em segundo plano ou a tela bloqueada: notificação fixa com o status e a ação Desconectar, e um wake lock parcial só enquanto `Authenticated`. Não tem lógica de conexão. Quem o inicia e para é `ForegroundConnectionController` (singleton, também criado com o `App`), seguindo `ConnectionStateDescriber.IsConnectedOrConnecting`. Parar é feito com um `startForegroundService` + `ActionStop` (o serviço chama `StartForeground` e se encerra), nunca com `StopService` direto — parar um serviço que ainda não chamou `StartForeground` derruba o app.

## Fala (TTS)

`NativeSpeechOutputService` (`Platforms/Android/NativeSpeechOutputService.cs`) usa `Android.Speech.Tts.TextToSpeech` nativo (aliased como `AndroidTts` para evitar ambiguidade com `Microsoft.Maui.Media.TextToSpeech`), implementando `AndroidTts.IOnInitListener` — por isso precisa herdar de `Java.Lang.Object`. Fila lógica simples: `priority = Now` → `QUEUE_FLUSH`; `Next`/`Normal` → `QUEUE_ADD`. `cancel` → `Stop()` + limpa pendências. `pause_speech` é best-effort (o TTS do Android não pausa de fato; só interrompe).

## Localização

`AppResources` (`NvdaRemoteDroid.Core/Resources/Strings/AppResources.Designer.cs` + `AppResources.resx`/`AppResources.en.resx`) é mantido manualmente (sem Visual Studio gerando o Designer) — ao adicionar uma chave, edite os dois `.resx` e a propriedade correspondente em `AppResources.Designer.cs`. Registrado no `LocalizationResourceManager.Maui` via `MauiProgram.cs`; XAML usa `{loc:Translate Key}`, C# usa `AppResources.Key`.

## Simplificações conhecidas da v1

Ver `README.md` na raiz do repo para a lista completa (chave do canal em `Preferences` em vez de `SecureStorage`, sem teste de integração direto para `TcpTlsTransportService.HandleLine`).
