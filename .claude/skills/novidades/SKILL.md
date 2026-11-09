---
name: novidades
description: Generate the bilingual (PT-BR + EN) marketing text the user posts in his WhatsApp/Telegram groups to announce NvdaRemoteDroid's new features, and save it to mk.txt at the repo root. Use when the user runs /novidades with a list of what's new (in any format — a dash-bulleted release-notes paragraph, free text, or a reference to specific features), or otherwise asks to write/update this kind of group-broadcast marketing text for a new version.
---

# Novidades (group marketing text)

Produces the recurring PT-BR + EN marketing message the user sends to his groups whenever he releases a new NvdaRemoteDroid version, written to `mk.txt` at the repo root. This is a separate, informal text from the official `distribution/whatsnew/whatsnew-pt-BR`/`whatsnew-en-US` files that ship inside the GitHub Release — don't confuse the two outputs, though the whatsnew files are a useful source of "what's already documented for this version" if `args` doesn't make that clear.

## Input

`args` is the user's raw description of what's new — often shaped like release notes (one or more `- ` bulleted items, e.g. the style in `distribution/whatsnew/whatsnew-pt-BR`), but may also be loose free text or just feature names. Treat it as the list of topics to cover, not as finished prose to paste in.

## The one rule that matters: write NEW text, never reuse old prose

Do **not** copy-paste wording from a previous `mk.txt`, from an example the user pastes as a style reference, or from `distribution/whatsnew/*`. Those are inputs for *what* to cover and *how it's formatted*, never text to recycle. Every run of this skill must produce original sentences describing the given novelties — even if a past message already covered a similar-sounding feature.

## Ground each item in the real feature before writing marketing copy

Before writing a line about a feature, verify what it actually does, so the copy is accurate — don't market from the bullet text alone:

- Check `NvdaRemoteDroid.Core/Resources/Strings/AppResources.resx` (PT-BR, the default culture) and `AppResources.en.resx` (EN) for the feature's existing UI strings (e.g. `Connection_*`, `Shell_Tab_*`, `Settings_*`, `RemoteControl_*`) — these give the precise, already-reviewed wording of what's exposed in the app, and the EN file doubles as a translation reference.
- When a feature isn't fully described by its UI strings, read the relevant ViewModel/Service directly (e.g. `RemoteConnectionStateMachine`, `TcpTlsTransportService`, `VirtualKeyboardView`/`KeyEventFactory`, `NativeSpeechOutputService`) to confirm the actual behavior before describing it.
- NvdaRemoteDroid has no subscription tiers or feature gating — it's entirely free. Never add a "(Pro)"-style tag; every feature is available to every user.
- If the version these novidades belong to isn't obvious from `args`, don't silently assume the repo's current `ApplicationDisplayVersion` in `NvdaRemoteDroid.csproj` is already public — check whether a matching `v*` tag/GitHub Release already exists, and ask the user which version this campaign is for if it's ambiguous.

## Output format

Write to `mk.txt` at the repo root (overwrite it), with this shape — every sentence should be freshly written for this run:

```
===========================
PT-BR
===========================

📢 <attention-grabbing header for this batch of novidades> 🎉

<one short intro line tying the items together, optional>

<emoji> <Feature name>
<1-3 original sentences: what it does, in plain terms, and why it helps someone controlling NVDA remotely as a blind/low-vision user.>

<repeat the emoji+feature block for each item in args>

🛠️ Melhorias de estabilidade, como sempre.   <- only if the input mentions stability/bug fixes

📲 Baixe a nova versão do NvdaRemoteDroid:

https://github.com/RafaelFernandesBR/NvdaRemoteDroid/releases/latest


===========================
EN
===========================

<same structure, translated — not a literal word-for-word translation, write it as natural English marketing copy conveying the same facts>
```

Notes on format:
- Pick a different emoji per feature based on what it actually is (🔌 for connection/relay features, ⌨️ for keyboard input, 🔊 for speech/TTS, 🔒 for certificate/trust features, ✅ for a simple capability, etc.) — don't reuse the same emoji for everything.
- The GitHub Releases link is the one constant that's always reused verbatim: `https://github.com/RafaelFernandesBR/NvdaRemoteDroid/releases/latest`. It points at whatever the latest signed release APK is, so don't hardcode a specific version number in the link.
- EN is a faithful second version, not an afterthought, but doesn't need to be longer or more detailed than the PT-BR one.
- The app is for blind and low-vision users remotely driving NVDA — accessibility/screen-reader framing (how a feature helps someone who can't see the controlled machine's screen) is usually the actual selling point, more than general appeal.

## After writing

Tell the user, briefly, which features you covered and where you grounded each one (the `AppResources` keys or code paths you checked), so they can spot anything you got wrong about behavior before sending it to the groups.
