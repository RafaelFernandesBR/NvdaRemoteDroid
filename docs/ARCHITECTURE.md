# Arquitetura — camadas e fluxo de dados

Detalhe das camadas citadas no `CLAUDE.md`. Leia aquele arquivo primeiro para o mapa geral; este documento é o aprofundamento de cada peça.

## Protocolo NVDA Remote

A engenharia reversa original do protocolo foi feita num relatório fora deste repositório (investigação do código-fonte do NVDA); o resumo abaixo é o que de fato foi implementado em `NvdaRemoteDroid.Core/Protocol/` — esta seção é a fonte da verdade sobre o que o app faz, não precisa do relatório original para dar manutenção aqui.

- **Transporte**: TCP + TLS 1.2. O app nunca fala direto com o NVDA — sempre com um relay (self-hosted pelo próprio NVDA, ou o público `nvdaremote.com:6837`).
- **Framing**: uma mensagem JSON UTF-8 por linha, terminada em `\n`, sem prefixo de tamanho. `LineFramer` implementa isso sobre `Stream` puro (testável com `MemoryStream`/stream fake de chunks pequenos), cuidando de caracteres UTF-8 multibyte partidos entre leituras.
- **Handshake**: `ProtocolVersionMessage(version: 2)` imediatamente após o TLS concluir, depois `JoinMessage(channel, "master")`. Sucesso → `ChannelJoinedMessage`. Senha errada → `ErrorMessage` seguido do relay fechando o socket.
- **Mensagens enviadas pelo master**: `KeyMessage`, `SendSasMessage`, `SetClipboardTextMessage`.
- **Mensagens recebidas e repassadas via `ITransportService.MessageReceived`**: `SpeakMessage`, `CancelMessage`, `PauseSpeechMessage`, `ToneMessage`, `SetClipboardTextMessage`, `MotdMessage`, `ErrorMessage` — todas com um `case` em `RemoteControlViewModelBase.OnMessageReceived`, então de fato produzem efeito na UI/TTS. `ChannelJoinedMessage` só dispara a transição de estado (`HandleLine`), não chega à ViewModel. **`ClientJoinedMessage`/`ClientLeftMessage` (presença de outros peers no canal) são parseadas e repassadas pelo evento, mas nenhuma ViewModel tem um `case` para elas hoje — são descartadas silenciosamente; não há notificação de presença na UI na v1.** `PingMessage` é ignorada de propósito, nunca respondida. `VersionMismatchMessage` também só dispara transição de estado.
- **Fora de escopo v1**: papel slave/follower, `generate_key` (só existe no relay público), braille (`display`/`braille_input`/`set_braille_info`), `wave` (caminho de arquivo do remetente, inútil cross-plataforma).

### Parser tolerante (`RemoteMessageParser`)

Não usa `[JsonPolymorphic]` — lê a propriedade `"type"` via `JsonDocument`, despacha por `switch` para `JsonSerializer.Deserialize<TConcreto>`. Dois motivos para isso:

1. O protocolo tolera campos extras desconhecidos (o NVDA real também tolera), então o parser nunca deve lançar por causa deles.
2. Um tipo de mensagem desconhecido (`index`, `nvda_not_connected`, `generate_key`) ou uma mensagem com campo obrigatório ausente (ex. `speak` sem `sequence`) vira `UnknownMessage(Type, RawJson)` — logado e descartado, nunca propaga exceção para quem chama `TryParse`.

### `speak.sequence` heterogêneo

`SpeechSequenceJsonConverter` lê cada item do array: uma string pura vira `SpeechTextItem`; um array `[ClassName, {attrs}]` vira `SpeechCommandItem` (classe desconhecida é ignorada com segurança, sem quebrar o parse da mensagem toda). `SpeechSequenceTextBuilder.ToUtterances` reduz isso a texto simples para o TTS: concatena os trechos de texto e trata `EndUtteranceCommand` como quebra de frase; qualquer outro comando (`LangChangeCommand`, `BreakCommand`, etc.) é ignorado — a v1 não tenta reproduzir prosódia.

## Transporte (`TcpTlsTransportService`)

Vive no projeto Android (`NvdaRemoteDroid/Services/TcpTlsTransportService.cs`), não no Core, porque usa `System.Net.Sockets`/`System.Net.Security` diretamente — não há nada Android-específico nele, só não foi extraído para o Core por não precisar ser testado com mocks de rede.

- `TcpClient { NoDelay = true }` + `SslStream` com `TargetHost` sempre setado e `SslProtocols.Tls12`.
- **TOFU (modo inseguro)**: `RemoteCertificateValidationCallback` aceita certificados válidos normalmente; em modo inseguro, calcula o fingerprint SHA-256 do certificado DER e compara contra `ICertificateTrustStore` (persistido via `Preferences`, chave `host:porta`). Se desconhecido, a validação retorna `false` — o que faz `AuthenticateAsClientAsync` **lançar** `AuthenticationException` (não retornar `false` silenciosamente). O catch específico para esse caso dispara o evento `CertificateTrustRequested`, aguarda a decisão do usuário via `TaskCompletionSource`, e — se aceito — refaz a conexão do zero recursivamente (o `SslStream` abortado é inutilizável).
- **Fila de envio**: um `Channel<string>` consumido por uma única task (`RunSendLoopAsync`) — evita que escritas concorrentes corrompam o stream.
- **Leitura**: um `LineFramer` **novo por tentativa de conexão** (nunca reaproveitado entre reconexões — ver gotcha no `CLAUDE.md`) alimenta `RemoteMessageParser.TryParse` → `HandleLine`, que decide transições de estado e repassa a mensagem via o evento `MessageReceived`.
- **Reconexão**: backoff fixo de 5s, só a partir de `ConnectionState.Error` (falha de IO/transporte). Um EOF "limpo" (o relay fecha o socket sem exceção) enquanto `Authenticated` também é tratado como `TransportFailed` — senão a UI ficaria dizendo "Conectado" para sempre depois de uma queda silenciosa.

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

1. **Teclado virtual** (`NvdaRemoteDroid/Controls/VirtualKeyboardView.cs`, construído em código C#, não XAML, para manter as ~60 teclas como uma tabela de dados). Modificadores Ctrl/Alt/Shift/Win são "sticky" de um tiro: ativa, afeta a próxima tecla tocada, desativa sozinho. O conjunto de modificadores ativos guarda `VirtualKeyMapping` completo (não só o `VkCode`) para não perder o bit `Extended` da tecla Windows.
2. **Teclado físico/Bluetooth**: `MainActivity.DispatchKeyEvent` intercepta `KeyEvent` só enquanto `Shell.Current.CurrentPage` é `RemoteControlPage` — nas demais telas (ex. `Entry` da chave de canal), o evento segue o fluxo normal do Android, senão digitar ali seria impossível. Mapeamento via `AndroidKeyToVirtualKeyMap` (tabela pura, testável, `Dictionary<AndroidKeyCode, VirtualKeyMapping>`).

## Fala (TTS)

`NativeSpeechOutputService` (`Platforms/Android/NativeSpeechOutputService.cs`) usa `Android.Speech.Tts.TextToSpeech` nativo (aliased como `AndroidTts` para evitar ambiguidade com `Microsoft.Maui.Media.TextToSpeech`), implementando `AndroidTts.IOnInitListener` — por isso precisa herdar de `Java.Lang.Object`. Fila lógica simples: `priority = Now` → `QUEUE_FLUSH`; `Next`/`Normal` → `QUEUE_ADD`. `cancel` → `Stop()` + limpa pendências. `pause_speech` é best-effort (o TTS do Android não pausa de fato; só interrompe).

## Localização

`AppResources` (`NvdaRemoteDroid.Core/Resources/Strings/AppResources.Designer.cs` + `AppResources.resx`/`AppResources.en.resx`) é mantido manualmente (sem Visual Studio gerando o Designer) — ao adicionar uma chave, edite os dois `.resx` e a propriedade correspondente em `AppResources.Designer.cs`. Registrado no `LocalizationResourceManager.Maui` via `MauiProgram.cs`; XAML usa `{loc:Translate Key}`, C# usa `AppResources.Key`.

## Simplificações conhecidas da v1

Ver `README.md` na raiz do repo para a lista completa (chave do canal em `Preferences` em vez de `SecureStorage`, sem Foreground Service dedicado para manter a conexão viva em segundo plano, sem teste de integração direto para `TcpTlsTransportService.HandleLine`).
