# OpenUTAU Multi Phonemizer

## 1) Overview

This plugin lets you set only one phonemizer on a track (`Master Router Phonemizer`) and route notes to sub-phonemizers using routing tags at the start of each lyric.

- Default routing syntax: `:phonemizerTag:lyric`
- Phonemizer tag source: the **second argument (tag)** in `[Phonemizer("Name", "Tag", ...)]`
- Default behavior: `Hard Boundary` at language boundaries (cuts VC/continuous connections)

---

## 2) Installation

1. Close OpenUtau.
2. Copy the files below to `\OpenUtau\Plugins`:
   - `MasterRouter.dll`
   - `master-router.config.json` (optional but recommended)
3. Start OpenUtau.
4. Select `Master Router Phonemizer` in track phonemizer settings.

Note:
- If you get a `file is being used by another process` error while replacing the DLL, OpenUtau is still running.

---

## 3) Routing Syntax

### Basic

- `:Phonemizer Name:Lyric`

Examples:
- `:JA VCV & CVVC:あ`
- `:EN VCCV:ah`
- `:KO CVC:가`

### Using aliases

You can map shorter tags with `aliases` in `master-router.config.json`.

Examples:
- `:ja:あ` -> `JA VCV & CVVC`
- `:en:hello` -> `EN VCCV`

Real usage examples:
<img width="1017" height="842" alt="Screenshot 2026-04-05 113849" src="https://github.com/user-attachments/assets/db3f0a72-a60b-4e57-b162-2603a8b7040d" />

<img width="1296" height="785" alt="Screenshot 2026-04-05 113909" src="https://github.com/user-attachments/assets/a55bd11c-ecc5-4336-b2ee-7e6335611821" />

---

## 4) Boundary Handling (Hard Boundary + Bridge)

Default rules:
- If the current note and neighboring note use different sub-phonemizers, that edge is treated as a boundary.
- At boundaries, `prev/next` linkage is cut to block VC/continuous transitions.

### Bridge syntax: `>`

If you append `>` at the end of a boundary note, you can provide a forward connection hint for the next note.

#### 4-1) Manual hint

- Format: `lyric>hint`
- Example: `:KO CVVC:각>k`
- Behavior: uses `k` as the next onset hint.

#### 4-2) Automatic hint

- Format: `lyric>`
- Example: `:KO CVVC:각>`
- Resolution order:
1. Use the first token from the next note's `phoneticHint`
2. If missing, estimate onset from the next lyric via language tables (currently JA/EN/KO)
3. If estimation fails, bridge is not applied (keeps default Hard Boundary)

---

## 5) Fallback Rules

When a routing tag is invalid or unregistered:

1. If the first valid lyric character is Hiragana/Katakana -> `jaFallback`
2. Otherwise -> `primary`

---

## 6) Config File (`master-router.config.json`)

Example:

```json
{
  "primary": "DEFAULT",
  "jaFallback": "JA VCV & CVVC",
  "quickTag": "JA VCV & CVVC",
  "aliases": {
    "ja": "JA VCV & CVVC",
    "en": "EN ARPA+",
    "ko": "KO CVVC",
    "default": "DEFAULT"
  }
}
```

Field descriptions:
- `primary`: default phonemizer tag
- `jaFallback`: fallback tag when lyric starts with Japanese script
- `quickTag`: default value for the batch tag-apply plugin
- `aliases`: user shorthand -> actual tag mapping

---

## 7) Supported / Unsupported Scope

### Recommended (Supported)
- Routing within the same singer format
  - Example: UTAU-family with UTAU-family, DiffSinger-family with DiffSinger-family
- Supports user-added custom phonemizers in addition to built-in phonemizers

### Not Recommended (Not planned)
- Mixed routing across different singer formats
  - Example: mixing UTAU phonemizers and DiffSinger phonemizers in one track

---

## 8) Troubleshooting

### Phonemizer does not appear in the list

1. Check DLL path: `\OpenUtau\Plugins\MasterRouter.dll`
2. Restart OpenUtau
3. Check under `More... -> General`
4. Check logs: `\OpenUtau\Logs\logYYYYMMDD.txt`

### DLL cannot be copied

- Fully close OpenUtau, then copy again
- If an OpenUtau process remains in the background, terminate it first

### Notice & Disclaimer
- This program was created with AI, 100% vibecoded, and only human-reviewed.
- There may be bugs where cross-phonemizer connections do not behave as intended.
- The developer is not responsible for any loss or damage caused by using this program.
