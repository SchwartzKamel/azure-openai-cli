# Stage Notes -- Keith Hernandez

> I'm Keith Hernandez. I have given this style of talk enough times to
> know what fails on stage and what fails in the green room. Read this
> the night before. Read the pre-flight the morning of.

## Pre-flight checklist (morning of)

- HDMI dongle in the bag. USB-C dongle in the bag. The conference
  cable is always the wrong one.
- Charger in the bag. The podium outlet is always behind a curtain.
- Demo machine fully charged anyway. Do not trust the outlet.
- Terminal font size at 22pt minimum. Verify from the back row of
  the room, not from the podium.
- Terminal is dark background, light text. Light backgrounds wash out
  on most projectors.
- `NO_COLOR=1` exported if the projector eats ANSI. Decide this
  during the AV check, not during the demo.
- Wi-Fi off. The demo does not need the network. Wi-Fi only invites
  notification popups and Slack toasts on stage.
- Do Not Disturb on. Calendar notifications off. Slack quit. Email
  quit. Your password manager browser extension closed.
- Browser tabs: zero. If you need a tab, you need a slide.
- Demo directory clean: `~/demo` empty, no shell history of real
  keys, scratch keyring confirmed empty.
- Pre-recorded asciinema fallbacks ready in a known path. Test the
  playback once. Speed factor 1.0x; do not let the cast race ahead.

## Things that fail live (and how I recover)

- **Keychain unlock prompt does not appear.** Means I am still
  unlocked from earlier. Acknowledge it: "macOS already trusts me
  this session. The first call would prompt." Move on.
- **`secret-tool` returns nothing.** D-Bus session is missing.
  Switch to the file-mode fallback and narrate it as the degraded
  path. Do not debug live. The demo machine has been pre-checked;
  if it fails on stage it is a session/D-Bus quirk and arguing with
  it from the podium loses the room.
- **Audience cannot read the terminal.** I will know within ten
  seconds because nobody laughs at the wizard joke. Stop. Bump font
  to 28pt. Apologize once. Continue.
- **Wi-Fi popup appears.** Cmd-tab away, dismiss, return. Do not
  comment on it; the room saw it and the room will forgive it once.
  Twice means I forgot to disable Wi-Fi.
- **The clicker dies.** Walk to the laptop. Use the keyboard. Do not
  ask the AV crew to find batteries during the talk.

## Voice

- "I'm Keith Hernandez" is the opener. Once. Not a catchphrase.
- Confident, not bragging. The pattern is the star of the talk, not
  me.
- One pause per slide. Silence is allowed. Silence is, in fact, how
  the takeaway slide lands.
- No "umm." No "so basically." No "as you can see." If they can see
  it, they do not need to be told they can see it.

## Q&A

- Repeat every question into the mic before answering. Half the room
  did not hear it.
- "I do not know" is a complete sentence. Follow with "find me in
  the hallway and we can dig in."
- If a question turns into a comment that turns into a speech, thank
  the commenter and move to the next hand.

## After

- Push the slides to the talk's repo within 24 hours. The audience
  remembers the talk for a week and the URL forever.
- File a retro note in `docs/talks/lolbin-credentials/` if anything
  in this checklist proved wrong on stage. Stage notes are a living
  document.
