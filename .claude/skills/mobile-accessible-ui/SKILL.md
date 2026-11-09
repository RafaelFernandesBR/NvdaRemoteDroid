---
name: mobile-accessible-ui
description: Guidance for visual/design decisions in NvdaRemoteDroid's native MAUI XAML screens — palette, typography, layout, spacing, and control choice — for a screen-reader-first app whose users are blind or low-vision NVDA users. Use when creating or restyling a Page, ContentView, or DataTemplate. Defaults to reusing existing Colors.xaml/Styles.xaml tokens and established layout patterns instead of introducing new visual choices. Does not cover CSS/web design or accessibility-testing/TalkBack QA procedure.
---

# Mobile Accessible UI

NvdaRemoteDroid is a native, screen-reader-first Android app: it lets a blind or low-vision user drive a remote NVDA session from the phone. Most people operating this app rely on TalkBack (or NVDA's own speech, relayed back) rather than looking at the screen, so this is the opposite of a job that rewards aesthetic risk-taking. The goal is **precise, consistent execution**: reuse what the app already has, and only deviate when a screen has a real, stated need the existing system can't meet. Never introduce something novel or elaborate just because it's possible — no bespoke drawing, no new motion, no new color or type scale invented on the spot.

## Scope

This skill governs **design decisions**: palette, typography, layout/XAML structure, spacing, and which control to reach for. It does not cover:
- Accessibility *mechanics* (`SemanticProperties`, `AutomationProperties`, localization of labels via `{loc:Translate}`/`AppResources`, `SemanticScreenReader.Default.Announce`) — those are mandated in `CLAUDE.md` and assumed as a baseline here, not re-explained.
- Accessibility *testing* (TalkBack QA, contrast verification tooling) — out of scope for this skill.

## Use the existing design system — don't invent one

- **Color**: only use `StaticResource`/`AppThemeBinding` pairs backed by `NvdaRemoteDroid/Resources/Styles/Colors.xaml` (`Primary`, `PrimaryDark`, `Secondary`, `Tertiary`, the `Gray100`–`Gray950` ramp, `White`/`Black`/`OffBlack`/`MidnightBlue`/`Magenta`). Never write a raw hex or named color (`"SeaGreen"`, `"White"`) directly in a page. As of now the existing pages (`ConnectionPage.xaml`, `RemoteControlPage.xaml`, `SettingsPage.xaml`, `TrustedCertificatesPage.xaml`) are clean of this kind of inline override — keep it that way; don't let new or edited screens introduce the drift. If a screen genuinely needs a color that isn't there, add a named token to `Colors.xaml` (with a light/dark pair) rather than inlining one.
- **Typography**: only `OpenSans` (Regular/Semibold, as registered in `MauiProgram.cs`), and the existing scale — the `Headline` and `SubHeadline` named styles in `Styles.xaml`, or the default size every control already gets from its implicit style. If a screen needs a new hierarchy level, add a keyed style to `Styles.xaml`, don't set `FontSize`/`FontAttributes` inline on an element.
- **Controls**: rely on the implicit (untyped) styles in `Styles.xaml` — every `Button`, `Entry`, `Switch`, `Picker`, etc. already has color, font, corner radius, and pressed/disabled states defined, plus `MinimumHeightRequest`/`MinimumWidthRequest = 44`. Setting `BackgroundColor`/`TextColor`/`FontAttributes`/`HeightRequest` inline on a control fights that system — avoid it in new or edited screens even though no such drift exists in the codebase today.

## Layout and control choice

- Mirror the structure of an existing comparable screen rather than inventing new page chrome. `ConnectionPage`, `SettingsPage`, and `TrustedCertificatesPage` are all `ScrollView > VerticalStackLayout` with `Padding`/`Spacing` set on the stack — follow that shape for a new form/settings-style screen.
- For a composite, stateful control, follow the pattern established by `Views/VirtualKeyboardView.xaml` (or wherever the on-screen keyboard lives): one accessible node per interactive key, modifier state reflected both visually and via `AutomationProperties`/`SemanticProperties`, decorative sub-elements marked `AutomationProperties.IsInAccessibleTree="False"` — instead of a new one-off template.
- There's no formal spacing-token scale in this app; when you need a `Padding`/`Spacing`/`ColumnSpacing` value, copy it from the nearest comparable existing screen rather than picking a new number.
- No custom `GraphicsView` drawing and no new animation/motion unless the task explicitly asks for it.

## Sizing: touch targets

`CLAUDE.md` requires `MinimumHeightRequest`/`MinimumWidthRequest = 44` on anything tappable — and that's already the implicit default every control gets from `Styles.xaml`, so there's no gap to compensate for here (unlike apps that ship a template default below their accessibility requirement). Just don't override it downward, and explicitly set `MinimumHeightRequest="44"`/`MinimumWidthRequest="44"` on any custom/non-styled element (e.g. a raw `Grid`-based tap target in `VirtualKeyboardView`) that doesn't inherit it from an implicit style.

## Color and contrast for low-vision users

- Prefer the app's existing light/dark `AppThemeBinding` pairs over a single flat color, so the choice holds up in both themes.
- Never communicate state (connected/disconnected, modifier active, error) through color alone. Pair any color change with a text/icon/border change — e.g. the connection status text itself (not just a colored dot) must say the state, and a sticky modifier key in `VirtualKeyboardView` should look visually distinct (border, fill) in addition to any color shift.
- If a genuinely new color is unavoidable, pick one that meets WCAG AA contrast against the surface it sits on (4.5:1 for normal text, 3:1 for large text or meaningful graphical elements).

## Accessibility semantics — decide, don't re-derive

Treat `CLAUDE.md`'s accessibility mechanics as given. This skill's job, when laying out a screen, is the design decision layered on top of that machinery: which elements carry meaning versus which are decorative, what heading level a label gets, how a composite element's (e.g. a keyboard key, a connection status row) accessible description is composed. Apply those decisions using the APIs already documented in `CLAUDE.md` — don't re-explain the APIs themselves here.

## When to ask vs. when to just decide

- **Decide and proceed, without asking**: any choice that's purely cosmetic and has an obvious answer from the existing system — which existing color/style/spacing value to reuse, which named style applies. Many users of this app can't evaluate these visually, so pausing to ask about them wastes time.
- **Ask first**: when a layout or style choice is genuinely ambiguous — two existing patterns could plausibly apply and the choice isn't obvious from context, or a screen has no close precedent in the app. Surface the specific choice and a recommendation, don't just pick one silently.

## Reference skeleton

A new settings-style screen, following the conventions above — no inline color/font overrides, tokens and localization reused throughout:

```xml
<ContentPage ...
             Title="{loc:Translate MyFeature_Title}">
    <ScrollView>
        <VerticalStackLayout Padding="20" Spacing="20">
            <Label Text="{loc:Translate MyFeature_Title}"
                   Style="{StaticResource Headline}"
                   SemanticProperties.HeadingLevel="Level1"/>

            <Label Text="{loc:Translate MyFeature_Description}"
                   TextColor="{AppThemeBinding Light={StaticResource Gray900}, Dark={StaticResource White}}"/>

            <Button Text="{loc:Translate MyFeature_Action}"
                    Command="{Binding DoActionCommand}"
                    SemanticProperties.Hint="{loc:Translate MyFeature_Action_Hint}"
                    AutomationProperties.Name="{loc:Translate MyFeature_Action_Name}"/>
        </VerticalStackLayout>
    </ScrollView>
</ContentPage>
```

The button has no `BackgroundColor`/`TextColor`/`HeightRequest`/`FontAttributes` — it inherits all of that, including the 44dp minimum, disabled state, and light/dark colors, from the implicit `Button` style. `MyFeature_*` keys would be added to `NvdaRemoteDroid.Core/Resources/Strings/AppResources.resx` (and the `.en.resx` translation) following the existing naming pattern (`Connection_Title`, `Connection_Description`, `Shell_Tab_*`, etc.).

## Restraint checklist

Before finishing a screen or component:
- Did every color, font, and size come from `Colors.xaml`/`Styles.xaml`, with no new inline value introduced without a stated reason?
- Is every new or resized interactive element ≥44dp?
- Is any state (connection status, modifier active, error) conveyed by color alone?
- Does the layout mirror an existing comparable screen's structure (`ConnectionPage`/`SettingsPage`/`TrustedCertificatesPage` for forms, `VirtualKeyboardView` for composite interactive controls)?
- Did you avoid adding any motion, animation, or custom drawing that wasn't asked for?
- Did every new display string go through `{loc:Translate}`/`AppResources`, with both PT-BR and EN entries added?
