---
name: bora
description: Commits pending NvdaRemoteDroid changes, bumps the app version, updates the whatsnew release notes, pushes to main, and pushes a new "vX.Y" tag to trigger the Release Android GitHub Actions workflow. Use when the user runs /bora, or otherwise asks to "ship"/"release"/"subir uma nova tag"/"publicar uma nova versão" after a round of code changes is done and verified.
---

# Bora (ship a new release)

Automates the git/release mechanics described in `CLAUDE.md`'s release workflow: commit, bump
version, update `distribution/whatsnew/*`, push `main`, tag, push tag. This is the *shipping*
step — it assumes the actual code change was already written, tested, and reviewed earlier in
the conversation (unit tests green, `code-reviewer` run, Android build passing). Do not use this
skill to skip that work; if no code review/testing happened yet for the pending changes in this
session, do that first and only then run this skill.

`args` (optional): the user's raw description of what changed in this release, in any shape —
free text, a list, or nothing at all. Use it as the source for the whatsnew bullets; if empty,
derive the bullets yourself from `git log <last-tag>..HEAD` and `git diff <last-tag>..HEAD
--stat` (and the current conversation's context about what was just built/fixed).

## Steps, in order

1. **Sanity-check the repo state.**
   - `git status` — confirm there is something to ship (staged/unstaged changes, or commits
     already made since the last tag that haven't been released). If there is truly nothing new
     since the last `v*` tag, say so and stop instead of creating an empty release.
   - If there are unstaged/uncommitted changes, review them the same way the "Committing
     changes with git" instructions describe (check `git status`/`git diff`, never blindly
     `git add -A` — exclude anything that looks like a stray/unrelated file or a secret).
   - Confirm the working branch is `main` (or ask if it's something else — this skill assumes
     the single-branch workflow this repo uses).

2. **Run the fast safety net.** `dotnet test NvdaRemoteDroid.Tests.Unit/NvdaRemoteDroid.Tests.Unit.csproj`
   must be 0 failures before continuing. This is cheap (seconds) and catches an obvious break
   that slipped through. Don't re-run the full Android build here — the release workflow itself
   does `dotnet publish -f net10.0-android -c Release`, so a local Android build is redundant
   with what CI is about to do; if that step was already run earlier in the conversation for
   these changes, there's no need to repeat it.

3. **Decide the next version.**
   - Read the latest `v*` tag (`git tag --sort=-v:refname` or `git describe --tags
     --abbrev=0`) and the current `<ApplicationDisplayVersion>`/`<ApplicationVersion>` in
     `NvdaRemoteDroid/NvdaRemoteDroid.csproj`.
   - Default bump: increment the minor number (`1.1` → `1.2`) and `ApplicationVersion` by 1 —
     this matches what was done for `v1.0` → `v1.1`. Only bump the major number if `args`
     explicitly says so (e.g. "major", "breaking") or the user says so directly.
   - If `args` names an explicit version (e.g. "sobe pra 2.0"), use that instead of
     auto-incrementing.
   - Edit `<ApplicationDisplayVersion>` and `<ApplicationVersion>` in
     `NvdaRemoteDroid/NvdaRemoteDroid.csproj` to match.

3b. **Rename/retitle note:** the new tag must be `v<ApplicationDisplayVersion>` (e.g. `v1.2`) —
    keep the tag and the csproj version in sync, since the GitHub Release title
    (`Release ${{ github.ref_name }}`) is derived straight from the tag name.

4. **Update the release notes.** Per `CLAUDE.md`: this is required, not optional — the release
   workflow's first step (`Prepare GitHub Release Notes`) does `cat
   distribution/whatsnew/whatsnew-pt-BR`/`whatsnew-en-US` and the job fails fast if either is
   missing or stale-looking. Overwrite both files (plain text, one bullet per line, `- ` prefix,
   PT-BR first since it's the app's default culture) with bullets describing *this* release only
   — not the full history. Write both the PT-BR and the EN version yourself (natural phrasing,
   not a literal word-for-word translation), grounded in what was actually built (check
   `AppResources.resx`/`.en.resx` for the user-facing strings touched, same sourcing approach the
   `novidades` skill uses) rather than paraphrasing commit messages.
   - This is a different, more terse/technical artifact than `novidades`'s `mk.txt` marketing
     text — don't conflate the two, and don't reuse `mk.txt` prose here if one exists.

5. **Commit.** Stage the version bump, the whatsnew files, and any pending code changes together
   (or as a sensible small set of commits if the pending changes are unrelated to each other —
   use judgment). Write a commit message describing the *why* of the shipped changes, ending
   with the attribution line from this session's system reminder when one is present.

6. **Push and tag.**
   ```
   git push origin main
   git tag -a v<version> -m "<one-line summary of this release>"
   git push origin v<version>
   ```
   Use an annotated tag (`-a`), matching the existing `v1.0`/`v1.1` tags in this repo.

7. **Confirm the workflow fired.** Run `gh run list --limit 5` and point out the queued/in-progress
   "Release Android" run for the new tag (and the "Tests" run for the `main` push). Don't wait
   around polling it to completion unless the user asks — report that it started and move on.

## Guardrails

- Never use `--no-verify`, `--force`, or rewrite existing tags/commits.
- If `dotnet test` fails, stop and report the failure instead of pushing/tagging anyway.
- If the repo has changes staged for *unrelated* work-in-progress that the user didn't intend to
  ship, ask before bundling them into the release commit.
- This skill pushes to the remote and triggers a signed release build — treat invoking `/bora`
  itself as the user's authorization for *this* run; it doesn't need a further "are you sure"
  beyond the normal tool-approval flow, but do surface exactly what's about to be pushed/tagged
  before doing it (e.g. a one-line summary of the version and commit) so the user can interrupt
  if something looks wrong.
