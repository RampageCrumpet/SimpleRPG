# Game audio workflow: Audacity MCP → Unity

How the project's sound effects were made, what failed, and what to do differently. Read this before making or changing any SFX. The longsword swings and the skeleton strike are the worked examples.

Written for agents that **cannot hear**. Every rule here exists because a sound was rejected by the user's ear after an agent judged it by guessing. The ear decides; your job is to plan, measure and make the ear's job easy.

## What exists

| Sound | Unity asset (`Assets/Art/Audio/SFX/Combat/`) | Length | Basis |
| --- | --- | --- | --- |
| Longsword A1, right diagonal | `SFX_Longsword_A1_RightDiagonal.wav` | 0.397 s | heaviest swing |
| Longsword A2, left diagonal | `SFX_Longsword_A2_LeftDiagonal.wav` | 0.295 s | lighter, quicker |
| Longsword A3, centerline thrust | `SFX_Longsword_A3_Thrust.wav` | 0.248 s | tight air puff, no scrape |
| Skeleton struck (standard hit) | `SFX_Skeleton_Hit_Strike_01.wav` | 0.420 s | crack, snap, delayed rattle |

All are mono, 44.1 kHz, 16-bit, no reverb (Unity handles room acoustics), approved by the user by ear. Nothing plays them yet: the MVP gap assessment notes there is no gameplay audio path (`MvpGapAssessment.md`). Unity import settings are untouched defaults.

## Where things live

Editable sources are outside the repository (back them up separately, as with the sallet).

| Purpose | Location |
| --- | --- |
| Longsword projects, review WAVs, recipes | `I:/Art/Sounds/Whoosh/` (`Attacks/`, `Review/`, `SOURCES.md`) |
| Skeleton strike project, review WAV, plan and build notes | `I:/Art/Sounds/Impacts/SkeletonStrike/` (`PLAN.md`) |
| Raw source clips | `I:/Art/Sounds/Source Sounds/` (`Sword Swings/`, `Bones/`, `Crunches/`) |
| Large licensed library (user's, do not modify) | `I:/Art/Sounds/Soniss GD2026/` (five zips, about 6.7 GB) |
| Analysis tool | `I:/Art/Sounds/Tools/sound_analysis.py` |

Each `PLAN.md` / `SOURCES.md` has the exact layers, trims, filters and gains. Rebuild from those, not from memory.

## Set up the Audacity MCP

- Server: [xdarkzx/audacity-mcp](https://github.com/xdarkzx/audacity-mcp), PyPI `audacity-mcp-server`, installed in its own venv at `~/.claude/tools/audacity-mcp/venv` (`uv tool install` failed on locked files; a plain venv worked). Registered as `audacity` in `~/.claude.json`.
- Works with **Audacity 3.x only** (4.x removed `mod-script-pipe`). Enable Edit → Preferences → Modules → `mod-script-pipe`, then **fully quit and reopen** Audacity. The pipe only loads at startup. No pipe means every call fails with `PIPE_NOT_FOUND`.
- Audacity and the agent must run as the same user. The server cannot start Audacity; you can with `Start-Process "C:\Program Files\Audacity\Audacity.exe"` (it crashed once mid-session and came back with an empty project).
- That venv also has numpy and PyAV, which is what the analysis tool uses.

## Working rules

1. **Plan before building.** Write timings and layers down first (see Planning). The user approves the plan.
2. **One candidate at a time.** Variants (head, limb, get-up, death) come only after the base is approved. Building three early wasted a round.
3. **Measure every export** before handing it over (see Measuring). Fix what the numbers show, then say what you changed.
4. **Review first, Unity last.** Export to a `Review/` folder next to the editable project. Copy into `Assets/` only after the user says approved, byte-identical, and verify with a hash.
5. **Say what you cannot verify.** You cannot hear. State the numbers and what they predict, and ask for the ear's verdict.
6. **Do not extract or move the user's library files** beyond what a task needs. Extract single files from zips into your own new folder.
7. **Do not delete the user's sources.** Recycle (not permanently delete) your own rejected work.

## Planning a sound from game data

Pull timings from the game, not from taste:

- `Assets/Items/Weapon Data/Longsword_Combo.asset`: per-attack `hitStart`/`hitEnd`, `duration`, `damageScalar` (A1 0.93-1.17 s dmg 1.0, A2 0.63-0.92 s dmg 0.79, A3 0.75-0.98 s dmg 0.84).
- `HitRegions.md`: Head ×1.75, Torso ×1.0, Arms/Legs ×0.75. A hit sound set should step with these.
- Swing sounds: put the loudest point at the middle of the hit window. Start playback at about 0.93 s (A1), 0.70 s (A2), 0.81 s (A3) into the attack clip.
- Hit sounds: no pre-roll. The strike must start within about 5 ms of the file start, because the trigger is the confirmed hit.
- Heavier and slower means lower, quicker and lighter means higher and shorter. Make each attack differ by layer-level choices, not by pitch-shifting one finished mix (the "fast" and "crisp" variants sounded identical).

## Sourcing sounds

- Raw clips that can be shaped beat pre-named sounds. "Skeleton hit" does not exist as a usable file; bone cracks, snaps, rattles and falling-bone clips do.
- **The Sonniss bundle is a sampler**: 2-4 audio files per pack, over 100 packs, nothing named bone or skeleton. Use it for generic impacts only.
- Inspect a file's *inside* before choosing it. A file summary says little: one 19 s clash file held seven separate hits, and most "crunches" were single 20 ms clicks. The tool's `--events` mode lists them.
- **License decision (user, 2026-10-24):** the `Bones` MP3s are Pixabay downloads (creator prefix plus number in the name). The project owner treats Pixabay sounds as functionally CC0 for this game: they will be used only as modified parts of game sounds, never trademarked and never sold as unmodified raw sounds outside the game. Do not re-flag these. The `Sword Swings` files carry Freesound IDs (the number prefix); their per-file license (CC0 versus attribution required) has **not** been checked, so confirm it before shipping and record it in `SOURCES.md`. New sources outside these two collections need their own check.
- Three source files used by the approved swings (`420626`, `109432`, `394457`) were deleted from `Sword Swings/` by the user afterwards and are in the Windows Recycle Bin. The finished WAVs and the `.aup3` projects (which embed the audio) are unaffected, but the recipes cannot be replayed from disk without restoring them.

## Measuring

Run the tool on every file you produce or are considering:

```
"C:/Users/Alex Van Oss/.claude/tools/audacity-mcp/venv/Scripts/python.exe" -I I:/Art/Sounds/Tools/sound_analysis.py FILE --events --split=0.22
```

| Number | Why it matters | Target |
| --- | --- | --- |
| onset | time to first strong sample | under 5 ms for a hit |
| end | last 3 ms | about 0, otherwise it clicks |
| peak | headroom | final files about -5 to -6 dBFS; above 0 means the source is clipped |
| centroid | brightness | 0.5-1.5 kHz boomy, **2-4 kHz dry and clacky**, 6 kHz and up hissy |
| 20 ms RMS | the real envelope | compare it to the shape you intended |
| energy before/after a split | which part dominates | rattle or tail well below the strike |

Always compare **energy**, not peaks. The first strike candidate had the rattle at an acceptable peak but it overlapped the strike and lasted too long, so it dominated by energy. Estimate gain from the actual events in the slice you chose, not from the file's loudest peak: the rattle slice was about 18 dB quieter than the file peak suggested.

## What failed and why

| Attempt | Result | Lesson |
| --- | --- | --- |
| Pink/white noise with volume envelope only | "just noise" | a whoosh needs a moving spectrum, volume alone is not enough |
| Wah-wah sweep on noise | "mechanical zwwoop" | narrow resonant sweeps read as synth effects, not air |
| Staff whoosh alone | "a rod moving through air" | add an edge layer for a blade (sliced sample) |
| Slice sample kept for 0.45 s | "scabbard grinding" | a plateau in a sample is a scrape; keep only the front of it |
| Metal clash samples for a skeleton | rejected | wrong material; bone is dry, not metallic |
| Wood-on-wood strikes for bone | "wooden dowel on dowel" | tonal, resonant wood reads as wood |
| Crunch clusters slowed 20-25% plus a sine thump | "too deep, like nothing" | a skeleton is light and clacky; do not pitch down |
| One 0.1 s filtered burst | "sharp noise" | a struck skeleton is a sequence: strike, then bones settling |
| Rattle at 70 ms, loud, long | "dominates" | it must start after the strike and fall away quickly |

What worked for the skeleton: natural-pitch crack cluster + sharp snap accent at the strike, then a quiet rattle entering after it, all filtered, 0.42 s.

## Audacity MCP quirks (verified)

Most of these cost real time. Check them before trusting a result.

- **`effect_adjustable_fade` ignores `fade_type` and always fades UP.** Use `effect_fade_out` and `effect_fade_in`. The longsword builds (and their first whoosh drafts) used the wrong tool, so the "fade-outs" in those recipes are fade-ins. Approved sounds are unchanged; see `Whoosh/SOURCES.md`.
- **`project_save` fails** ("write to a file" error, timeout). Use `project_save_as` with a full path.
- **Every Save As truncates the previously bound `.aup3`** to about 100-250 KB. Copy the project trio (`.aup3`, `-wal`, `-shm`) to a backup right after saving, and restore later. Verify with SQLite `PRAGMA integrity_check` and `select count(*) from sampleblocks` on a scratch copy.
- **`edit_copy` then `edit_paste` does nothing.** To delay a layer, import it starting earlier in the source, `edit_silence` the lead-in, then `effect_fade_in`.
- **`generate_*` needs an existing selected track and a time selection** (`select_region`); without them it fails or leaves a hidden dialog open.
- **`track_set_properties` acts on the focused track, not the index you pass.** Gain and `track_mute` do not apply. Use `effect_amplify`, and have only one track in the project when you export.
- **`track_remove` removes the selected tracks.** Select explicitly with `select_tracks` and verify with `track_get_info`.
- **`project_export_audio` refuses to overwrite.** Delete your own earlier export first. Exports append `LIST`/`id3` chunks after the audio; parse the chunks.
- **Run calls one at a time.** Parallel calls collide.
- **Modal dialogs block the pipe** until the user clicks (for example Resample, error dialogs). A call timing out at 30 s usually means a dialog is open. Ask the user.
- **Importing a long, high-rate file** (a 3 minute 192 kHz WAV) risks the same timeout. Use the smaller files.
- Imports keep their own sample rate; exports are 44.1 kHz. Resampling is not needed.

## Build recipe for one layer

Import the source, `select_region` and `edit_delete` the end, then the front (leave 3-4 ms before the onset), `track_get_info` to confirm the length, filters first (`effect_high_pass_filter` / `effect_low_pass_filter`), then fades, then `effect_amplify`. Filter before shaping the envelope. Repeat per layer, then `project_save_as`, `select_all`, `analyze_find_clipping`, export, measure.

## Open work

- Head (×1.75) and limb (×0.75) variants of the skeleton strike, then skeleton **get up** and **death**. Candidate sources are already chosen: `falling-bones` (collapse), `rattling-bones` (get up), `bones.wav` ticks, `bone-break-ver-2` (brighter crack).
- A hollow body "knock" under the crack was left out. Add it only if the strike is judged thin.
- Rebuild the three longsword swings with true fade-outs, if the user wants to compare.
- Verify the Freesound licenses behind the longsword swings (IDs 422513, 420626, 109432) and record them in `I:/Art/Sounds/Whoosh/SOURCES.md`.
- Add the audio path to gameplay (play on `DamageResolvedEvent` for hits, in the attack clips for swings), set Unity import settings (mono, short clips as decompress-on-load), commit the new assets.
