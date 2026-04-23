# Slide Outline -- LOLBin Credentials

**Slot:** 25-30 minutes (talk) + 5 minutes Q&A.
**Slide count:** 20.
**Total time budget:** 27 minutes (within slot, with a 3-minute pad).

Each slide is text-only here. Visual direction is one line. Speaker
notes are 2-3 sentences. Time budgets sum at the end.

---

**Slide 1: Title**

- Visual: Title card. Talk title, speaker name, event, date.
- Speaker notes: "Hi, I'm `<Speaker Name>`. The next half hour is
  about where to put the API key. That is the whole talk." Land the
  joke; let the room settle.
- Time budget: 0.5 min.

**Slide 2: The awkward question**

- Visual: A single line of text: "Where does the API key go?"
- Speaker notes: Frame the universal CLI problem. Every author hits it.
  Most authors get it wrong on the first try, and so did I.
- Time budget: 1.0 min.

**Slide 3: The wrong answers (a list)**

- Visual: Three bullets, each with a strikethrough: dotfile, env var,
  bundled crypto.
- Speaker notes: Walk each. Dotfile leaks via screenshots and backups.
  Env vars leak via `ps`, child processes, and crash dumps. Bundled
  crypto bloats the binary and reinvents wheels.
- Time budget: 1.5 min.

**Slide 4: What the OS already ships**

- Visual: Three logos / names side by side: libsecret, Keychain,
  Credential Manager.
- Speaker notes: Every modern OS ships a credential vault. They are
  tested, audited, and integrated with the lock screen. They are also,
  conveniently, scriptable from a CLI.
- Time budget: 1.0 min.

**Slide 5: "Living off the land"**

- Visual: Definition card. "LOLBin: a binary the OS already ships,
  used as part of your tool's normal operation."
- Speaker notes: Borrow the term from the security community. Same
  shape: do not bring your own; use what is already on the box.
- Time budget: 1.0 min.

**Slide 6: The shape of the solution**

- Visual: A diagram with three boxes: CLI, OS vault, network. Arrow
  from CLI to vault on read; arrow from CLI to network on use.
- Speaker notes: The CLI never persists the secret itself. It reads
  from the vault on demand and holds it in process memory only.
- Time budget: 1.5 min.

**Slide 7: The three shell-outs**

- Visual: A 3-row table. Linux: `secret-tool`. macOS: `security`.
  Windows: `cmdkey` / `Get-StoredCredential`.
- Speaker notes: One command per platform. Read-only at the OS layer.
  No new dependencies in the binary itself.
- Time budget: 1.5 min.

**Slide 8: Detection at runtime**

- Visual: A short pseudocode block:

  ```text
  if linux and which secret-tool: use libsecret
  elif mac and which security:    use keychain
  elif windows:                   use cred manager
  else:                           fallback
  ```

- Speaker notes: Detection is `which` plus an OS check. Cheap, runs
  once at startup, no platform conditionals leak past this layer.
- Time budget: 1.5 min.

**Slide 9: The first-run wizard**

- Visual: A mock terminal: prompts for endpoint, key, model.
- Speaker notes: First run is the only place the user sees the secret.
  Five prompts, one confirmation, never written to disk in plaintext.
  This is the UX moment that earns trust.
- Time budget: 1.0 min.

**Slide 10: Demo -- intro**

- Visual: Static slide that says "Demo." Black background, large text.
- Speaker notes: Set expectations. "Three beats: wizard, Linux vault,
  macOS vault. About nine minutes. If anything breaks, I have a
  pre-recorded fallback and we keep moving."
- Time budget: 0.5 min.

**Slide 11: Demo -- live**

- Visual: Switch to terminal. (No slide content.)
- Speaker notes: Run the demo per `demo-script.md`. Do not deviate.
- Time budget: 9.0 min.

**Slide 12: What just happened**

- Visual: Three bullets recapping the demo beats.
- Speaker notes: Recap is for the people who looked away during the
  demo to take a note. Three sentences, then move on.
- Time budget: 1.0 min.

**Slide 13: The fallback path**

- Visual: A flowchart: vault present -> use vault. Vault absent ->
  warn -> file mode (plaintext, mode 0600).
- Speaker notes: Headless CI boxes do not have keyrings. The CLI must
  degrade, must warn loudly, and must not silently store plaintext.
- Time budget: 1.5 min.

**Slide 14: Failure modes that bite**

- Visual: A four-row table. Locked keyring. SSH session with no
  D-Bus. WSL. Corporate MDM that disables `security` access.
- Speaker notes: Each row is a real bug report. Each is solvable, and
  each costs you a half day the first time. Plan for them up front.
- Time budget: 1.5 min.

**Slide 15: The audit story**

- Visual: A single sentence: "Where is the key? Ask the OS."
- Speaker notes: A side benefit: audit answers are short. The vault
  is the source of truth. Your CLI is not.
- Time budget: 1.0 min.

**Slide 16: NativeAOT considerations**

- Visual: A short before/after: binary size with bundled crypto vs
  without.
- Speaker notes: Shelling out keeps the binary lean. AOT trim
  warnings stay quiet. Cold start stays under target.
- Time budget: 1.0 min.

**Slide 17: What this is not**

- Visual: Three honest disclaimers in a list.
- Speaker notes: Not a hardware-token story. Not a multi-tenant
  secret manager. Not a substitute for OIDC where OIDC fits. Pick the
  right tool; this one fits single-user CLIs.
- Time budget: 1.0 min.

**Slide 18: Decision tree**

- Visual: A small decision tree: "Single user? CLI? Single binary?"
  -> use this pattern. Otherwise -> see the alternatives slide.
- Speaker notes: Audience can take a photo of this slide and use it
  on Monday morning. That is the goal.
- Time budget: 1.0 min.

**Slide 19: Takeaway**

- Visual: One sentence, large: "Your CLI should not own the secret.
  The OS already does."
- Speaker notes: Read it. Pause. Do not add anything.
- Time budget: 0.5 min.

**Slide 20: Thanks + Q&A**

- Visual: Speaker contact, repo URL, talk URL placeholder.
- Speaker notes: "Questions. I am also in the hallway after this."
- Time budget: 0.5 min (then Q&A on the conference clock).

---

## Time budget total

0.5 + 1.0 + 1.5 + 1.0 + 1.0 + 1.5 + 1.5 + 1.5 + 1.0 + 0.5 + 9.0 +
1.0 + 1.5 + 1.5 + 1.0 + 1.0 + 1.0 + 1.0 + 0.5 + 0.5 = **27.0 minutes.**

Fits a 25-30 minute slot with a 3-minute pad. If the slot is exactly
25 minutes, cut Slide 15 (the audit story; nice-to-have, not
load-bearing) and trim the demo wrap by 30 seconds.
