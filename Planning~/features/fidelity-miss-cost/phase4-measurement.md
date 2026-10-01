# Phase 4 — A/B measurement, `feat-fidelity-miss-cost` (#65 §5.1)

- **before:** `5957fdf 5957fdf`
- **after:** `worktree at 3367808+dirty`
- **gate:** default `minScore` = 0.6

## 4A — Regression: the committed corpus, unmodified

### 4A-i — as the parser sees it (fixture level, F7)

Replayed **15** fixtures (`silence_negative` has no final and contributes none; `split_…`'s two finals are rejoined, as the utterance buffer rejoins them before parsing).

**Delta is exactly zero.** Every fixture is an exact phrase match at this level, so no required literal is missed and the constant is never reached. This is the regression check passing — and it is precisely why it is not evidence ABOUT the change, which is what 4B is for. It also answers **F7**: 0 of the 16 cases in `Tests~/Fixtures/audio/manifest.json` change `expectedIntent` or `expectedSlots`.

### 4A-ii — bridge-level finals, replayed individually

The design (§7, A1.4) states that *all* committed transcripts are exact phrase matches. That is true of 15 of the 16 fixtures. It is **not** true of `split_…`, whose two finals are each half of one command by construction — so the corpus is not quite as inert as the design assumed. Recorded here because the claim was load-bearing in the argument that F8's original plan was vacuous.

| fixture | final | before | after | crosses 0.6? |
|---|---|---|---|---|
| `split_close_distance_safe_range_target_hotel_one.wav` | `close distance safe range` | set_distance_named 0.3000 | set_distance_named 0.4000 | no |
| `split_close_distance_safe_range_target_hotel_one.wav` | `target hotel one` | approach_target 0.5000 | approach_target 0.6667 | **yes** |

These halves only reach the parser in isolation if the pause exceeds the buffer window — the case the fixture exists to exercise. Where it does, `target hotel one` now clears the gate as `approach_target`. That is §5.1 behaving exactly as designed (a 3-element pattern, one dropped literal, `2/3`), applied to half a command; it is named here rather than buried because it is the one place the committed corpus shows the change at all.

## 4B — Single-word ablation of those same transcripts

**61** variants — every word of every committed transcript deleted once, except the single `[unk]` token (the scorer never charges it, so removing it is not the degradation under study).

| outcome | count |
|---|---:|
| **newly clears `minScore`** (was rejected, now fires) | **10** |
| winning intent changes among visible results | **0** |
| newly emitted as a sub-threshold candidate (A1 ruling 2 residue) | 0 |
| score rises, still under the gate | 22 |
| unchanged | 29 |

Only the first row is observable at default settings: **10 of 61** ablated real transcripts go from silently rejected to recognised.

### Newly clears the gate — recovers the intended command

**9 of 10** rescues fire the same intent the undamaged transcript fires. These are the utterances the feature exists for: a real transcript minus one word, silently rejected before, correctly recognised after.

| fixture | dropped | ablated utterance | before | after | Δ |
|---|---|---|---|---|---:|
| `approach_target_alpha_one.wav` | `approach` | `target alpha one` | approach_target 0.5000 | approach_target 0.6667 | +0.1667 |
| `approach_target_alpha_one.wav` | `target` | `approach alpha one` | approach_target 0.5000 | approach_target 0.6667 | +0.1667 |
| `switch_to_navigation.wav` | `switch` | `to navigation` | mode_navigation 0.5000 | mode_navigation 0.6667 | +0.1667 |
| `switch_to_navigation.wav` | `to` | `switch navigation` | mode_navigation 0.5000 | mode_navigation 0.6667 | +0.1667 |
| `set_heading_two_seven_zero.wav` | `set` | `heading two seven zero` | set_heading 0.5000 | set_heading 0.6667 | +0.1667 |
| `set_heading_two_seven_zero.wav` | `heading` | `set two seven zero` | set_heading 0.5000 | set_heading 0.6667 | +0.1667 |
| `switch_to_weapons.wav` | `switch` | `to weapons` | mode_weapons 0.5000 | mode_weapons 0.6667 | +0.1667 |
| `switch_to_weapons.wav` | `to` | `switch weapons` | mode_weapons 0.5000 | mode_weapons 0.6667 | +0.1667 |
| `switch_to_weapons.wav` | `weapons` | `switch to` | mode_weapons 0.5000 | mode_weapons 0.6667 | +0.1667 |

### Newly clears the gate — fires a DIFFERENT command

**1 of 10.** This is the sharpest edge of §5.1 and it is reported separately because it is not a win. Where the dropped word is the *discriminator* between sibling patterns, the surviving evidence fits both siblings equally, the score clears the gate, and registration order picks the winner — so a command fires where nothing fired before, and it may be the wrong one. Note this is the FLUSH path: issue #70's tail guard closed exactly this shape at the EAGER gate, but a final transcript that genuinely ends there is not in progress and no tail rule applies.

| fixture | dropped | ablated utterance | fires | undamaged utterance fires | after |
|---|---|---|---|---|---:|
| `switch_to_navigation.wav` | `navigation` | `switch to` | **mode_weapons** | mode_navigation | 0.6667 |

### Winning intent changes

Reordering among already-visible results. Each one needs arguing on its own merits; an empty table here means selection order was genuinely untouched.

_None._

### Newly emitted sub-threshold candidates

The accepted residue of Amendment A1 ruling 2: raising `rawScore` by 0.5 per miss lifts these over the parse loop's `<= 0f` floor, so they appear as extra low-scoring results in later extraction rounds. All are far below `minScore`, so nothing changes at recogniser level — but a user running `minScore` under ~0.35 would start seeing them.

_None._

### Score rises, still rejected

The cost is halved, not abolished — these still fail, which is §5.1 working as designed rather than a partial fix.

| fixture | dropped | ablated utterance | before | after | Δ |
|---|---|---|---|---|---:|
| `cease_fire.wav` | `cease` | `fire` | cease_fire 0.2500 | cease_fire 0.5000 | +0.2500 |
| `cease_fire.wav` | `fire` | `cease` | cease_fire 0.2500 | cease_fire 0.5000 | +0.2500 |
| `resume_fire.wav` | `resume` | `fire` | cease_fire 0.2500 | cease_fire 0.5000 | +0.2500 |
| `resume_fire.wav` | `fire` | `resume` | resume_fire 0.2500 | resume_fire 0.5000 | +0.2500 |
| `navigation_mode.wav` | `navigation` | `mode` | mode_weapons 0.2500 | mode_weapons 0.5000 | +0.2500 |
| `navigation_mode.wav` | `mode` | `navigation` | mode_navigation 0.2500 | mode_navigation 0.5000 | +0.2500 |
| `enable_all.wav` | `enable` | `all` | mode_all 0.2500 | mode_all 0.5000 | +0.2500 |
| `enable_all.wav` | `all` | `enable` | mode_all 0.2500 | mode_all 0.5000 | +0.2500 |
| `disable_all.wav` | `disable` | `all` | mode_all 0.2500 | mode_all 0.5000 | +0.2500 |
| `disable_all.wav` | `all` | `disable` | mode_disable 0.2500 | mode_disable 0.5000 | +0.2500 |
| `launch_jackals_target_hotel_two.wav` | `launch` | `jackals target hotel two` | launch_weapon 0.6250 | launch_weapon 0.7500 | +0.1250 |
| `launch_jackals_target_hotel_two.wav` | `target` | `launch jackals hotel two` | launch_weapon 0.6250 | launch_weapon 0.7500 | +0.1250 |
| `launch_one_missiles_target_hotel_one.wav` | `launch` | `one missiles target hotel one` | launch_weapon 0.7000 | launch_weapon 0.8000 | +0.1000 |
| `launch_one_missiles_target_hotel_one.wav` | `target` | `launch one missiles hotel one` | launch_weapon 0.7000 | launch_weapon 0.8000 | +0.1000 |
| `fall_back_from_target_bravo_two.wav` | `fall` | `back from target bravo two` | retreat_from_target 0.7000 | retreat_from_target 0.8000 | +0.1000 |
| `fall_back_from_target_bravo_two.wav` | `back` | `fall from target bravo two` | retreat_from_target 0.7000 | retreat_from_target 0.8000 | +0.1000 |
| `fall_back_from_target_bravo_two.wav` | `from` | `fall back target bravo two` | retreat_from_target 0.7000 | retreat_from_target 0.8000 | +0.1000 |
| `fall_back_from_target_bravo_two.wav` | `target` | `fall back from bravo two` | retreat_from_target 0.7000 | retreat_from_target 0.8000 | +0.1000 |
| `fire_two_torpedoes_at_bravo_two.wav` | `fire` | `two torpedoes at bravo two` | launch_weapon 0.7000 | launch_weapon 0.8000 | +0.1000 |
| `fire_two_torpedoes_at_bravo_two.wav` | `at` | `fire two torpedoes bravo two` | launch_weapon 0.7000 | launch_weapon 0.8000 | +0.1000 |
| `filler_launch_three_torpedoes_target_alpha_three.wav` | `launch` | `[unk] three torpedoes target alpha three` | launch_weapon 0.7000 | launch_weapon 0.8000 | +0.1000 |
| `filler_launch_three_torpedoes_target_alpha_three.wav` | `target` | `[unk] launch three torpedoes alpha three` | launch_weapon 0.7000 | launch_weapon 0.8000 | +0.1000 |

