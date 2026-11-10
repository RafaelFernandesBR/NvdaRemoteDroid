# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

NvdaRemoteDroid is a .NET MAUI Android app that remotely controls **NVDA** (the Windows screen reader) using the **NVDA Remote** protocol, acting only as the **master/leader** role (the phone drives a remote NVDA; this app never acts as the controlled machine). It supports both a direct connection (self-hosted relay, `host:port`) and the public relay (`nvdaremote.com:6837`) with a channel key. Target framework is **`net10.0-android`** — Android is the only shipping platform. Comments and many identifiers are in **Portuguese** in the existing code; match that when editing. User-facing UI text is localized (PT-BR + EN) via `LocalizationResourceManager.Maui` — never hardcode display strings.

## Commands

```bash
dotnet restore                                                              # restore NuGet packages
dotnet build -f net10.0-android                                             # debug build
dotnet publish NvdaRemoteDroid/NvdaRemoteDroid.csproj -f net10.0-android -c Release -p:AndroidPackageFormats=apk  # release APK
dotnet test NvdaRemoteDroid.Tests.Unit/NvdaRemoteDroid.Tests.Unit.csproj     # unit tests (net10.0, seconds, no emulator)
```

- Requires the MAUI Android workload: `dotnet workload install maui-android`.
- **Tests**: `NvdaRemoteDroid.Core` (`net10.0`, no Maui/Android) hosts the entire portable slice of the app — the protocol parser/serializer, the connection state machine, the Android-key-to-Windows-VK map, the speech-sequence-to-text builder, the ViewModel base classes. `NvdaRemoteDroid.Tests.Unit` (xUnit + NSubstitute + Shouldly) runs via plain `dotnet test`, no `maui-android` workload or emulator needed. Don't try to unit-test anything that stays in the `NvdaRemoteDroid` project itself (Pages, `Platforms/Android/*`, the concrete `TcpTlsTransportService`) the same way — it needs the Android workload/a real device/emulator; only the extracted pure logic is covered.
- **Required workflow after code changes**, strictly in this order: (1) `dotnet test NvdaRemoteDroid.Tests.Unit/NvdaRemoteDroid.Tests.Unit.csproj` (0 failures) — this validates the protocol/state-machine/keyboard logic that actually matters, so prefer adding/updating a test over just eyeballing a change under `NvdaRemoteDroid.Core`; (2) run the `code-reviewer` agent/skill over the generated diff and fix legitimate findings; (3) only once both of those are clean, run `dotnet build -f net10.0-android` (0 errors) as the final gate before delivering — don't build earlier in the loop just to "check," since the Android build is the slow step and the first two steps already catch most issues.
- CI (`.github/workflows/tests.yml`) on every push/PR to `main` (and manually via `workflow_dispatch`): runs `NvdaRemoteDroid.Tests.Unit` (no Android workload/emulator) and uploads the `.trx` results as an artifact.
- CI (`.github/workflows/release-android.yml`) on every `v*` tag push: builds and publishes a signed release APK to a GitHub Release. No AAB, no Play Store upload (out of scope for now). Signing secrets required in the repo: `KEYSTORE_BASE64`, `KEYSTORE_PASSWORD`, `KEY_ALIAS`, `KEY_PASSWORD`. **Before tagging, update the release notes** in `distribution/whatsnew/whatsnew-pt-BR` and `whatsnew-en-US` (plain text, one bullet per line) — the workflow concatenates both into the GitHub Release body for that version.

## Architecture

MVVM using **CommunityToolkit.Mvvm**. ViewModels derive from `ObservableObject` and use `[ObservableProperty]`/`[RelayCommand]` source generators.

**Dependency injection** is centralized in `MauiProgram.cs`. Pages and ViewModels are transient; state services (`ITransportService`, `ICertificateTrustStore`, `IConnectionPreferencesService`, `IMotdSeenStore`, `IClipboardService`, `ISessionFeedback`, `IRemoteSessionCoordinator`) are singletons; `ISpeechOutputService` and `ForegroundConnectionController` are Android-only (`#if ANDROID`). `IRemoteSessionCoordinator` and `ForegroundConnectionController` are injected into `App`'s constructor purely so they exist from startup. Register anything new there or DI resolution fails at navigation time.

**Navigation** is Shell-based (`AppShell.xaml`, `TabBar` with Connection/Control/Settings). `TrustedCertificatesPage` is a non-tab route registered in `AppShell.xaml.cs` via `Routing.RegisterRoute`.

**Layer map** (full detail in [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — read it before touching any of these):

- **Protocol** (`NvdaRemoteDroid.Core/Protocol/`): one `record` per NVDA Remote message type, a hand-written `RemoteMessageParser` (never throws — unknown types/missing required fields become `UnknownMessage`; `channel_joined`/`client_joined`/`client_left` are read field-by-field and only require `type`), a custom `JsonConverter` for the heterogeneous `speak.sequence` array, and `LineFramer` for the `\n`-delimited JSON-per-line framing.
- **Transport** (`NvdaRemoteDroid/Services/TcpTlsTransportService.cs`): `TcpClient` + `SslStream` (TLS 1.2), TOFU certificate trust in insecure mode, a `Channel<string>` send queue, and reconnection with a fixed 5s backoff — but only from transport failures, never from the terminal `AuthFailed`/`VersionMismatch` states.
- **Connection state machine** (`NvdaRemoteDroid.Core/Transport/RemoteConnectionStateMachine.cs`): pure, table-tested transitions driving the UI's status text and the transport's auto-reconnect decision.
- **Session** (`NvdaRemoteDroid.Core/Session/RemoteSessionCoordinator.cs`): singleton that owns everything received from NVDA (speech, clipboard, tone, motd) and channel presence. ViewModels never handle incoming protocol messages.
- **Keyboard** (`NvdaRemoteDroid.Core/Keyboard/`): `AndroidKeyToVirtualKeyMap` (Android keycode → Windows VK + `extended` flag), `KeyEventFactory` (always emits a down/up pair, `scan_code: null`), `VirtualKeyboardLayout` (pure table of the on-screen key groups and ready-made shortcuts). Two input paths: the on-screen `VirtualKeyboardView` (one key group at a time, three-state sticky modifiers) and the physical/Bluetooth keyboard via `MainActivity.DispatchKeyEvent` (only intercepted while `RemoteControlPage` is the visible page, the session is authenticated and no text field has focus).
- **Background** (`Platforms/Android/RemoteConnectionForegroundService.cs` + `ForegroundConnectionController.cs`): `specialUse` foreground service with a persistent notification, started/stopped from the transport state.
- **Speech** (`ISpeechOutputService` / `NativeSpeechOutputService`): native `Android.Speech.Tts.TextToSpeech` (not MAUI Essentials, which has no `QUEUE_FLUSH`/pause), with a small priority-aware logic queue.
- **Localization**: `AppResources` (generated, hand-maintained — `NvdaRemoteDroid.Core/Resources/Strings/`) + `LocalizationResourceManager.Maui`; `{loc:Translate Key}` in XAML.

## Conventions & gotchas

- **Folder names do not reliably match the on-disk layout vs. role** — e.g. `TrustedCertificatesViewModel` lives directly under `NvdaRemoteDroid/ViewModels/` with no Core base class (unlike `Connection`/`RemoteControl`/`Settings`, which all have a `*ViewModelBase` in Core). Follow the `namespace`/base-class pattern already present in the file you're editing.
- `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` are on in every project.
- `KeyMessage.ScanCode` must always be `null` — the follower (NVDA) recalculates it and ignores whatever is sent; never populate it "for completeness."
- `ConnectionState.AuthFailed` and `ConnectionState.VersionMismatch` are **terminal** — `RemoteConnectionStateMachine.ShouldAutoReconnect` is `false` for both. Don't let a `TransportFailed` event move the state machine out of either; wrong channel password must not trigger an infinite reconnect loop hammering the relay.
- `TcpTlsTransportService.ConnectOnceAsync` creates a **new `LineFramer` per connection attempt** — never hoist it to an instance field; a `LineFramer` reused across a dropped connection carries stale partial bytes into the next attempt's first message.
- `Android.Speech.Tts.TextToSpeech` collides with `Microsoft.Maui.Media.TextToSpeech` (CS0104) in any file that uses MAUI Essentials elsewhere — use the `AndroidTts` alias already established in `NativeSpeechOutputService.cs`.
- `NativeSpeechOutputService` implements `AndroidTts.IOnInitListener`, a Java-bound interface — it must derive from `Java.Lang.Object` (plain C# classes can't satisfy `IJavaPeerable`/`IJavaObject`).
- `VirtualKeyboardView`'s active-modifier set is `HashSet<VirtualKeyMapping>`, not `HashSet<VirtualKeyCode>` — the `Extended` bit (e.g. the Windows key needs `extended: true`) must survive from the modifier button press to the eventual `SendKeyDownAsync`/`SendKeyUpAsync` calls.
- `RemoteMessageParser` must turn **any** JSON object with `"type":"channel_joined"` into a `ChannelJoinedMessage`, whatever the other fields look like — a rejected `channel_joined` leaves the connection stuck in `Handshaking` with the whole keyboard disabled (real field bug against the public relay). Never log a message's values, only its shape (`RemoteMessageShape`): the channel key travels in it.
- `TcpTlsTransportService.SendAsync` silently drops messages unless `State == Authenticated` and must never throw for connection-state reasons (callers are `async void` UI handlers). Handshake messages bypass it and go straight to the queue.
- Stop `RemoteConnectionForegroundService` only through `ForegroundConnectionController` (start intent with `ActionStop`), never `StopService` right after `StartForegroundService` — Android kills the app if a service started that way is stopped before calling `StartForeground`.
- Test classes that set `AppResources.Culture` or assert localized text must be in `[Collection(CultureCollection.Name)]` — the culture is static and xUnit runs classes in parallel.
- The channel key (room password) is stored in `Preferences` (app-private, unencrypted), not `SecureStorage` — a deliberate v1 simplification (it's a room password, not an account credential); see `README.md` for the full list of known v1 simplifications.
- Accessibility is the point of the app — keep `AutomationProperties.Name`/`HelpText` and `SemanticProperties` intact on every interactive control, keep `MinimumHeightRequest`/`MinimumWidthRequest = 44` on anything tappable, and announce state changes via `SemanticScreenReader.Default.Announce(...)`.
