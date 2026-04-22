# Swag Brief

> I'm Keith Hernandez. This is what a vendor needs to quote a job, and what a maintainer needs to know before saying yes to a conference booth. No art yet — the brief unblocks the art.

**Owner:** Keith Hernandez (DevRel)
**Clearance:** Jackie Chiles (licensing / trademark) — *every art file must be reviewed before it hits a vendor*
**Last reviewed:** 2026-04-22

---

## 1. What assets exist today

| Asset | Status | Path | Use |
|---|:-:|---|---|
| Project wordmark "az-ai" / "azure-openai-cli" | ✅ text-only | n/a | Any print surface; plain-text typesetting fine |
| Kramer-designed logo mark | ⚠️ raster only, no vendor-ready SVG | `img/` *(unconfirmed inventory — see §2)* | Screen only until §2 closes |
| Hero GIF (`its_alive_too.gif`) | ✅ | `img/its_alive_too.gif` | Booth demo loop (screen only, not print) |
| Color palette | ❌ not defined | — | See §3 proposal |
| Typography spec | ❌ not defined | — | See §3 proposal |
| Sticker die-cut templates | ❌ | — | Vendor blocker |
| Laptop-cover templates (13" / 14" / 16") | ❌ | — | Vendor blocker |
| Pull-up banner / backdrop | ❌ | — | Out of scope until first sponsored booth |

## 2. What's missing (placeholder — to land in a follow-up)

Before the first vendor quote goes out, create `docs/devrel/swag-assets/` (not committed yet) with:

- `logo-mark.svg` — vector original, outlined paths, no embedded raster
- `logo-mark-mono.svg` — single-ink variant for one-color print
- `logo-wordmark.svg` — text-only lockup for narrow spaces
- `logo-combined.svg` — mark + wordmark, horizontal + stacked
- `logo-mark-@1x.png`, `@2x.png`, `@3x.png` — for web use
- `logo-mark-print-300dpi.png` — CMYK-safe export for print
- `palette.md` — hex + RGB + CMYK + Pantone equivalents (see §3)
- `typography.md` — display + body font, license, fallback stack

**Until `logo-mark.svg` exists, no sticker / apparel / pull-up-banner order ships.** Screen-only uses (slide decks, booth loop, website) may use the existing raster with Jackie's sign-off on the specific deployment.

## 3. Brand constraints (proposed — ratify before first print)

### Color palette (proposal)

> To be ratified by Russell Dalrymple (UX) and Jackie Chiles (trademark). Numbers are placeholders — update once the palette PR lands.

| Role | Hex | RGB | CMYK | Pantone (nearest) |
|---|---|---|---|---|
| Primary | `#0A0A0A` *(near-black terminal ground)* | 10, 10, 10 | 0, 0, 0, 96 | Black 6 C |
| Accent | `#00D787` *(prompt green)* | 0, 215, 135 | 71, 0, 78, 0 | 354 C |
| Secondary | `#E6E6E6` *(terminal foreground)* | 230, 230, 230 | 0, 0, 0, 10 | Cool Gray 1 C |
| Warning | `#FFB020` | 255, 176, 32 | 0, 35, 93, 0 | 137 C |

Rationale: every demo in the repo renders on a dark terminal; swag reads cohesively with what people see on stage.

### Minimum sizes

- **Logo mark, print:** 12 mm / 0.47 in across on stickers; 20 mm / 0.79 in on apparel
- **Logo mark, screen:** 32 px square minimum; render at 2× or 3× for Retina
- **Wordmark, print:** 28 mm / 1.10 in minimum on any single-line layout

### Clear space

All logo variants: 0.5× the cap-height of the wordmark on every side. Don't crowd it.

### Don't

- No rotating, skewing, or outlining the mark
- No recolors outside the palette without clearance
- No placement on photography or busy backgrounds — use the mono variant if the substrate isn't flat
- No Seinfeld character art on swag — that's internal persona shorthand, not public brand (Jackie's remit, hard "no")
- No Microsoft / Azure logos on any swag we hand out — we're Azure-adjacent, not Azure-branded (COI hygiene)

## 4. Trademark / licensing clearance

Every art file, every vendor order, and every merchandise listing passes Jackie Chiles before it's placed. The checklist:

1. Is every piece of art original, or under a license that permits merchandise use? (Stock photos are rarely merch-licensed. Icon sets usually aren't either.)
2. Does the art use any third-party trademark (Microsoft, OpenAI, Azure, Docker, .NET, etc.)? **Default: remove it.** Approved exceptions require written sponsor-agreement language.
3. Is the typeface embedded or outlined? Webfont licenses don't cover print.
4. Is the color choice likely to be confused with a trademarked competitor's palette? (Jackie has veto.)
5. Does the piece include a URL? If yes, that URL must still resolve in 24 months. Prefer `github.com/SchwartzKamel/azure-openai-cli` over vanity domains.

See also [`docs/licensing-audit.md`](../licensing-audit.md).

## 5. Vendor specs (ready to paste into a quote request)

### Stickers

- **Material:** vinyl, matte finish, UV-resistant laminate, outdoor-grade
- **Sizes:** 3" circle, 2" circle, 4"×2" rectangle die-cut
- **Art:** SVG with outlined paths; 300 dpi PNG fallback at 2× target size
- **Bleed:** 0.125" (3.2 mm)
- **Safe area:** 0.125" inside the cut line
- **Color:** full CMYK; spot color if the vendor offers it for the accent green
- **Quantity:** start with 250 of each for a first regional meetup run

### T-shirts (booth giveaway)

- **Blank:** unisex tri-blend, S–3XL range, neutral heather color
- **Print:** single-color screen print, accent green on dark garment OR near-black on light garment
- **Placement:** left-chest mark (2" wide) *or* full-chest wordmark (9" wide) — pick one per run
- **Quantity:** 50 for a meetup; 150 for a conference booth

### Laptop-cover stickers (large format)

- **Sizes:** 13", 14", 16" (MacBook / standard laptop lids)
- **Material:** textured matte vinyl, residue-free removable adhesive
- **Art:** same SVG kit; composed per-size with safe-area respected around the Apple logo cutout

## 6. Distribution policy (events)

> Distribute swag like you'd offer a good seat at a diner — warmly, without pressure, and never in exchange for an email.

- **No lead-capture requirement.** We do not trade stickers for badge scans. If an event form forces lead capture on table items, we don't put swag on the table.
- **Ask before applying.** Never stick a sticker on anyone's laptop for them. Hand it over. Let them choose.
- **Kids at family-friendly events:** one sticker per hand, no T-shirts (we don't stock child sizes), a "ask your grown-up" script for anything else.
- **Accessibility:** keep a low-height item (stickers) reachable for wheelchair users; the T-shirt pile on the high end of the table is fine, the primary giveaway is not.
- **Shipping by request:** if a community member emails asking for a sticker, we ship one for free within reason. We do not offer bulk-ship to re-distributors.

## 7. Lead-time (planning backwards from an event)

| Activity | Lead time before event |
|---|---|
| Event confirmed + booth slot committed | 8 weeks |
| Art direction locked, Jackie clearance complete | 6 weeks |
| Vendor quote, artwork proof approved | 4 weeks |
| Production run complete, shipped to hotel / venue / warehouse | 2 weeks |
| On-site setup (pack list + booth-in-a-box arrival check) | 1 day |
| Post-event debrief memo (under `docs/talks/<event>/debrief.md`) | +1 week after event |

**Do not commit to a booth inside the 6-week window without explicit Jackie-cleared assets on hand.** The lead-time is the brief.

## 8. Booth-in-a-box (kit manifest — placeholder)

First full kit ships after the first sponsored event. Expected contents, for planning:

- 1× pull-up banner (retractable, 33" × 80")
- 1× tablecloth (6 ft, printed single-color on dark)
- 250× stickers (mix of 3" and 2")
- 50× T-shirts (size-balanced; `S:8, M:15, L:15, XL:8, 2XL:3, 3XL:1`)
- 1× demo laptop with `az-ai-v2` installed and the booth loop GIF running
- 1× HDMI-C adapter, 1× USB-C power brick, 1× 10 ft extension cord, 1× power strip
- 1× printed one-pager with project URL, QR code to GitHub, COI disclosure visible
- 1× laminated pack list (this list)

Update this section when the first kit is actually built; the numbers are a planning estimate.

---

— *Keith Hernandez. Swag is a promise. Ship it like one.*
