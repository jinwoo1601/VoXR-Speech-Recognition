# Facts verified on the main thread (2026-09-05)

These resolve claims that disagree between the research reports. Treat them as authoritative over R1–R6 where they conflict.

## Shipped Android `libvosk.so` exports (from `nm -D Runtime/Plugins/Android/arm64-v8a/libvosk.so`)

`vosk_recognizer_*` symbols present: `accept_waveform`, `accept_waveform_f`, `accept_waveform_s`, `final_result`, `free`, `new`, `new_grm`, `new_spk`, `partial_result`, `reset`, `result`, `set_grm`, `set_max_alternatives`, `set_nlsml`, `set_partial_words`, `set_spk_model`, `set_words`.

**Not exported**: `vosk_recognizer_set_endpointer_mode`, `vosk_recognizer_set_endpointer_delays`. (R3's statement that these are callable at runtime is wrong for the shipped binary; R1 is right.) Consequence: endpointer tuning on device goes through `conf/model.conf`, not a runtime call, unless `libvosk.so` is rebuilt from a newer vosk-api.

## Header / binary mismatch

`NativeBridge~/include/vosk_api.h` declares `set_max_alternatives` (line 29), `set_partial_words` (31), `set_endpointer_mode` (33), `set_endpointer_delays` (34) but does **not** declare `vosk_recognizer_set_grm`, which the binary exports. Any bridge change that calls `set_grm` must add the declaration (or vendor the 0.3.45 header).

## The shipped model (`NativeBridge~/vendor/vosk-model-small-en-us-0.15`, identical file list to the StreamingAssets zip in the host project)

Files: `am/final.mdl`, `conf/mfcc.conf`, `conf/model.conf`, `graph/Gr.fst`, `graph/HCLr.fst`, `graph/disambig_tid.int`, `graph/phones/word_boundary.int`, `ivector/{final.dubm,final.ie,final.mat,global_cmvn.stats,online_cmvn.conf,splice.conf}`.

- **The model uses online i-vectors** (the `ivector/` directory) — relevant to speaker-adaptation proposals.
- **No `graph/words.txt`**, yet grammar mode is in production and tested; VOSK reads the word symbol table from the FST for this model family. R2's "missing words.txt" observation is not a defect.
- `conf/model.conf` verbatim:

```
--min-active=200
--max-active=3000
--beam=10.0
--lattice-beam=2.0
--acoustic-scale=1.0
--frame-subsampling-factor=3
--endpoint.silence-phones=1:2:3:4:5:6:7:8:9:10
--endpoint.rule2.min-trailing-silence=0.5
--endpoint.rule3.min-trailing-silence=0.75
--endpoint.rule4.min-trailing-silence=1.0
```

So the endpointer silence thresholds (0.5 / 0.75 / 1.0 s) and `--lattice-beam=2.0` are editable in a text file that ships inside the model zip; the package's `ModelExtractor` unpacks the zip at first run. Both an Editor `libvosk.dll` (`Runtime/Plugins/x86_64/`) and the Android `.so` read the same conf.

## Existing but unused bridge capabilities (from the code audit, confirmed)

`vosk_bridge_start_push` / `vosk_bridge_push_audio` / `vosk_bridge_get_input_level` are bound in C# with zero runtime call sites (used only by the verification tiers); `vosk_bridge_has_result` has no C# binding; the AAudio capture backend is dead code.
