// ============================================================================
// Purpose:  Desktop WAV->transcript harness — replays the fixture corpus through
//           the desktop bridge in push mode and compares final transcripts
//           against the committed expectation baseline (Tier C)
// Layer:    NativeBridge.Harness
// Owns:     (executable entry point, no public types)
// Depends:  WavData, LoadWav, vosk_bridge C ABI, RingBuffer
// ============================================================================

#include "vosk_bridge.h"
#include "ring_buffer.h"
#include "wav_reader.h"

#include <nlohmann/json.hpp>

#include <algorithm>
#include <array>
#include <chrono>
#include <cstdio>
#include <filesystem>
#include <fstream>
#include <iterator>
#include <optional>
#include <set>
#include <string>
#include <thread>
#include <vector>

using nlohmann::json;
using nlohmann::ordered_json;

namespace {

// The bridge's init rate: decoder samples per second (the dump's decoderRate).
constexpr int kDecoderRate = 16000;

constexpr const char* kBaselineComment =
    "Bridge-level committed baseline for the Tier C harness (see harness/README.md). "
    "Relationship to Tests~/Fixtures/audio/manifest.json: that file carries PARSER-level expectations "
    "(expectedIntent/expectedSlots; expectedTranscript deliberately empty for filler/split/silence cases), "
    "while expectedFinals here are the raw final transcripts the desktop bridge emits — [unk] tokens included — "
    "under the exact grammar/gain/libvosk pinned in this file. "
    "frontEndSamples and frontEndDigest pin the front end's output per fixture before padding "
    "(its int16 sample count and its 64-bit FNV-1a digest as 16 lowercase hex digits); "
    "they are harness-written and host-scoped (see harness/README.md). "
    "'file' is a basename resolved against --fixtures; the corpus manifest's same-named key is Tests~/Fixtures/-relative. "
    "Per the Tier B F7-amendment policy, raw transcripts belong to this committed baseline: "
    "regenerating the corpus (pause-lead cases included), bumping libvosk or changing the front end "
    "re-baselines via --write-baseline as a conscious, reviewed step. "
    "grammar is the verbatim output of VoxrCommandParser.GenerateGrammarJson over the demo grammar "
    "(contiguous pattern-literal runs + slot values + alias keys, each as a multi-word phrase entry AND as its individual words, "
    "plus digit vocabulary + confirm/cancel follow-up words + [unk], ordinal-sorted).";

struct FixtureOutcome {
    std::string file;
    std::vector<std::string> expected;
    std::vector<std::string> actual;
    std::vector<std::string> errors;
    bool pass = false;
    size_t pushed = 0;
    size_t prevPushed = 0;
    size_t seq = 0;
    int64_t consumed = 0;
    std::optional<uint64_t> expectedFrontEndSamples;
    std::optional<std::string> expectedFrontEndDigest;
    uint64_t frontEndSamples = 0;
    std::string frontEndDigest;
    bool frontEndPass = false;
};

// Opt-in --dump sink: JSON lines, one record per line, LF only (binary
// stream), key order as built. Inert unless --dump set `enabled`; every
// write sits under that bit, and nothing here touches stdout.
struct DumpWriter {
    std::ofstream out;
    bool enabled = false;

    void Write(const ordered_json& record) {
        out << record.dump(-1, ' ', false, json::error_handler_t::replace) << '\n';
    }
};

DumpWriter dump_writer;

struct PacedTrace {
    uint32_t chunk = 0;
    std::chrono::steady_clock::time_point t0;
    std::vector<std::array<int64_t, 3>> pushes, reads;
};

PacedTrace paced;

int64_t Micros() {
    return std::chrono::duration_cast<std::chrono::microseconds>(std::chrono::steady_clock::now() - paced.t0)
        .count();
}

void DumpCase(const std::string& file, size_t samples, uint32_t sample_rate) {
    ordered_json rec;
    rec["kind"] = "case";
    rec["case"] = file;
    rec["samples"] = samples;
    rec["sampleRate"] = sample_rate;
    dump_writer.Write(rec);
}

void DumpCaseEnd(const FixtureOutcome& r) {
    ordered_json rec;
    rec["kind"] = "case-end";
    rec["case"] = r.file;
    rec["results"] = r.seq;
    rec["pushed"] = r.pushed;
    rec["consumed"] = r.consumed;
    rec["errors"] = r.errors;
    if (paced.chunk != 0) {
        rec["pushes"] = paced.pushes;
        rec["reads"] = paced.reads;
    }
    dump_writer.Write(rec);
}

void DrainResults(FixtureOutcome& r) {
    int is_final = 0, length = 0;
    const char* p = nullptr;
    while ((p = vosk_bridge_get_result(&is_final, &length)) != nullptr) {
        std::string payload(p, static_cast<size_t>(length));
        if (dump_writer.enabled) {
            const int64_t wall = paced.chunk != 0 ? Micros() : 0;
            int64_t consumed = 0, origin = 0;
            vosk_bridge_get_result_position(&consumed, &origin);
            ordered_json rec;
            rec["kind"] = "result";
            rec["case"] = r.file;
            rec["seq"] = r.seq;
            rec["final"] = is_final != 0;
            rec["pushed"] = r.pushed;
            rec["prevPushed"] = r.prevPushed;
            rec["consumed"] = consumed;
            rec["origin"] = origin;
            if (paced.chunk != 0)
                rec["wall"] = wall;
            ordered_json parsed = ordered_json::parse(payload, nullptr, false);
            if (parsed.is_discarded())
                rec["raw"] = payload;
            else
                rec["payload"] = std::move(parsed);
            dump_writer.Write(rec);
        }
        ++r.seq;
        json j = json::parse(payload, nullptr, false);
        if (j.is_discarded()) {
            r.errors.push_back("unparseable result: " + payload);
            continue;
        }
        if (j.contains("error")) {
            r.errors.push_back(payload);
            continue;
        }
        if (is_final) {
            std::string text = j.value("text", "");
            if (!text.empty())
                r.actual.push_back(text);
        }
    }
    r.prevPushed = r.pushed;
}

int64_t Poll(FixtureOutcome& r) {
    const int64_t consumed = vosk_bridge_get_consumed();
    const int64_t at = Micros();
    if (consumed != (paced.reads.empty() ? 0 : paced.reads.back()[0]))
        paced.reads.push_back({consumed, at, static_cast<int64_t>(r.pushed)});
    DrainResults(r);
    return at;
}

// Pushes the whole buffer with clamped-write pacing: a short write means the
// ring is full, so drain results and back off briefly. The stall guard is a
// liveness backstop only — verdicts never depend on timing.
bool PushAll(const float* data, size_t count, FixtureOutcome& r) {
    size_t offset = 0;
    int stalled = 0;
    while (offset < count) {
        uint32_t chunk = static_cast<uint32_t>(std::min<size_t>(16384, count - offset));
        int written = vosk_bridge_push_audio(data + offset, chunk);
        if (written < 0) {
            r.errors.push_back("vosk_bridge_push_audio returned error " +
                               std::to_string(written));
            return false;
        }
        offset += static_cast<size_t>(written);
        r.pushed += static_cast<size_t>(written);
        if (static_cast<uint32_t>(written) < chunk) {
            if (written == 0 && ++stalled > 2000) { // ~10 s without progress
                r.errors.push_back("recognition thread stalled (ring never drained)");
                return false;
            }
            if (written > 0)
                stalled = 0;
            DrainResults(r);
            std::this_thread::sleep_for(std::chrono::milliseconds(5));
        } else {
            stalled = 0;
        }
    }
    return true;
}

bool PushPaced(const float* data, size_t count, FixtureOutcome& r) {
    size_t offset = 0;
    while (offset < count) {
        const size_t stop = offset + std::min<size_t>(paced.chunk, count - offset);
        const int64_t end = static_cast<int64_t>(r.pushed + (stop - offset));
        const int64_t due = (end * 1000000 + 47999) / 48000;
        int64_t at = Poll(r);
        int64_t since = std::max(due, at);
        while (true) {
            if (at >= due) {
                int written = vosk_bridge_push_audio(data + offset, static_cast<uint32_t>(stop - offset));
                if (written < 0) {
                    r.errors.push_back("vosk_bridge_push_audio returned error " +
                                       std::to_string(written));
                    return false;
                }
                offset += static_cast<size_t>(written);
                r.pushed += static_cast<size_t>(written);
                if (offset == stop) {
                    paced.pushes.push_back({end, due, at});
                    break;
                }
                if (written > 0) {
                    since = at;
                } else if (at - since > 10000000) {
                    r.errors.push_back("recognition thread stalled (ring never drained)");
                    return false;
                }
            }
            std::this_thread::yield();
            at = Poll(r);
        }
    }
    return true;
}

template <class Pause, class Stalled>
std::string WaitFrontEndBy(uint64_t samples, uint64_t* out_output_samples, uint64_t* out_digest, Pause pause,
                           Stalled stalled) {
    uint64_t seen = 0;
    int rc = vosk_bridge_get_frontend_stats(&seen, out_output_samples, out_digest,
                                            nullptr, nullptr, nullptr);
    while (rc == VOSK_BRIDGE_OK && seen != samples) {
        pause();
        uint64_t now = 0;
        rc = vosk_bridge_get_frontend_stats(&now, out_output_samples, out_digest,
                                            nullptr, nullptr, nullptr);
        const bool moved = now != seen;
        if (moved)
            seen = now;
        if (stalled(moved))
            return "front end stalled: input " + std::to_string(now) + " of " +
                   std::to_string(samples) + " samples";
    }
    if (rc != VOSK_BRIDGE_OK)
        return "vosk_bridge_get_frontend_stats failed with code " + std::to_string(rc);
    return "";
}

std::string WaitFrontEnd(uint64_t samples, uint64_t* out_output_samples, uint64_t* out_digest) {
    return WaitFrontEndBy(samples, out_output_samples, out_digest,
                          [] { std::this_thread::sleep_for(std::chrono::milliseconds(5)); },
                          [n = 0](bool moved) mutable { if (moved) n = 0; return !moved && ++n > 2000; });
}

std::string WaitFrontEndPaced(uint64_t samples, uint64_t* out_output_samples, uint64_t* out_digest,
                              FixtureOutcome& r) {
    int64_t at = Micros(), since = at;
    return WaitFrontEndBy(samples, out_output_samples, out_digest,
                          [&] { std::this_thread::yield(); at = Poll(r); },
                          [&](bool moved) { if (moved) since = at; return at - since > 10000000; });
}

bool SpinConsumed(FixtureOutcome& r) {
    int64_t at = Poll(r), since = at;
    size_t seen = paced.reads.size();
    while (true) {
        const int64_t consumed = paced.reads.empty() ? 0 : paced.reads.back()[0];
        if (consumed == static_cast<int64_t>(r.pushed))
            return true;
        if (paced.reads.size() != seen) {
            seen = paced.reads.size();
            since = at;
        } else if (at - since > 10000000) {
            r.errors.push_back("drain stalled: consumed " +
                               std::to_string(consumed) + " of " +
                               std::to_string(r.pushed) + " pushed");
            return false;
        }
        std::this_thread::yield();
        at = Poll(r);
    }
}

std::string HexDigest(uint64_t digest) {
    char buf[17];
    std::snprintf(buf, sizeof(buf), "%016llx", static_cast<unsigned long long>(digest));
    return buf;
}

int Fail(const std::string& msg) {
    std::fprintf(stderr, "vosk-bridge-harness: %s\n", msg.c_str());
    return 2;
}

} // namespace

int RunHarness(int argc, char** argv) {
    std::string model_dir, fixtures_dir, manifest_path, dump_path;
    std::optional<std::string> paced_arg;
    bool write_baseline = false;

    for (int i = 1; i < argc; ++i) {
        std::string arg = argv[i];
        if (arg == "--model" && i + 1 < argc) model_dir = argv[++i];
        else if (arg == "--fixtures" && i + 1 < argc) fixtures_dir = argv[++i];
        else if (arg == "--manifest" && i + 1 < argc) manifest_path = argv[++i];
        else if (arg == "--write-baseline") write_baseline = true;
        else if (arg == "--dump" && i + 1 < argc) { dump_path = argv[++i]; dump_writer.enabled = true; }
        else if (arg == "--paced" && i + 1 < argc) paced_arg = argv[++i];
        else return Fail("unknown or incomplete argument: " + arg + "\nusage: vosk-bridge-harness --model <dir> --fixtures <dir> --manifest <expectations.json> [--write-baseline] [--dump <file> [--paced <samples>]]");
    }
    if (model_dir.empty() || fixtures_dir.empty() || manifest_path.empty())
        return Fail("--model, --fixtures and --manifest are all required");
    if (paced_arg) {
        if (!dump_writer.enabled)
            return Fail("--paced needs --dump");
        if (write_baseline)
            return Fail("--paced cannot write the baseline");
        const std::string& value = *paced_arg;
        const bool digits = !value.empty() && value.size() <= 5 &&
                            std::all_of(value.begin(), value.end(), [](char ch) { return ch >= '0' && ch <= '9'; });
        const unsigned long chunk = digits ? std::stoul(value) : 0;
        if (chunk < 1 || chunk > 16384)
            return Fail("--paced takes a chunk of 1 to 16384 samples: " + value);
        paced.chunk = static_cast<uint32_t>(chunk);
    }

    const std::string conf_path = model_dir + "/conf/model.conf";
    std::optional<std::string> conf;
    {
        std::ifstream cf(conf_path, std::ios::binary);
        if (cf)
            conf = std::string(std::istreambuf_iterator<char>(cf), std::istreambuf_iterator<char>());
    }

    std::ifstream mf(manifest_path);
    if (!mf)
        return Fail("cannot open manifest: " + manifest_path);
    json manifest = json::parse(mf, nullptr, false);
    if (manifest.is_discarded())
        return Fail("manifest is not valid JSON: " + manifest_path);

    if (!manifest.contains("cases") || !manifest["cases"].is_array())
        return Fail("manifest has no 'cases' array: " + manifest_path);
    if (manifest["cases"].empty())
        return Fail("manifest has zero cases — refusing to report a vacuous green run: " + manifest_path);
    if (!manifest.contains("gain") || !manifest["gain"].is_number())
        return Fail("manifest 'gain' missing or not a number (required decode-regime pin): " + manifest_path);
    if (!manifest.contains("grammar") || !manifest["grammar"].is_array() ||
        manifest["grammar"].empty())
        return Fail("manifest 'grammar' missing or empty (required — free decode would invalidate the baseline): " + manifest_path);
    for (const auto& w : manifest["grammar"]) {
        if (!w.is_string())
            return Fail("manifest 'grammar' must be an array of strings: " + manifest_path);
    }
    if (!manifest.contains("libvosk") || !manifest["libvosk"].is_string() ||
        manifest["libvosk"].get<std::string>().empty())
        return Fail("manifest 'libvosk' pin missing (record the provisioned libvosk version): " + manifest_path);
    if (!write_baseline) {
        if (!manifest.contains("modelConf") || !manifest["modelConf"].is_string())
            return Fail("manifest 'modelConf' pin missing or not a string (record " + conf_path + " with --write-baseline): " + manifest_path);
        if (!conf || *conf != manifest["modelConf"].get<std::string>())
            return Fail("model conf " + conf_path + " is absent or differs from the manifest 'modelConf' pin: " + manifest_path);
    } else if (!conf) {
        return Fail("cannot pin 'modelConf': model conf " + conf_path + " is absent (the pin has no null form): " + manifest_path);
    }
    for (const auto& c : manifest["cases"]) {
        if (!c.is_object() || !c.contains("file") || !c["file"].is_string())
            return Fail("every manifest case needs a string 'file': " + manifest_path);
        if (c.contains("expectedFinals")) {
            if (!c["expectedFinals"].is_array())
                return Fail("case '" + c["file"].get<std::string>() + "': expectedFinals must be an array: " + manifest_path);
            for (const auto& e : c["expectedFinals"]) {
                if (!e.is_string())
                    return Fail("case '" + c["file"].get<std::string>() + "': expectedFinals entries must be strings: " + manifest_path);
            }
        }
        if (c.contains("frontEndSamples") && !c["frontEndSamples"].is_number_unsigned())
            return Fail("case '" + c["file"].get<std::string>() + "': frontEndSamples must be a non-negative integer: " + manifest_path);
        if (c.contains("frontEndDigest") && !c["frontEndDigest"].is_string())
            return Fail("case '" + c["file"].get<std::string>() + "': frontEndDigest must be a string: " + manifest_path);
    }

    {
        std::set<std::string> listed;
        for (const auto& c : manifest["cases"])
            listed.insert(c["file"].get<std::string>());
        std::error_code ec;
        std::filesystem::directory_iterator dir(fixtures_dir, ec);
        if (ec)
            return Fail("cannot enumerate fixtures dir: " + fixtures_dir + " (" + ec.message() + ")");
        std::string unlisted;
        for (const auto& entry : dir) {
            if (entry.path().extension() == ".wav" &&
                listed.count(entry.path().filename().string()) == 0)
                unlisted += (unlisted.empty() ? "" : ", ") + entry.path().filename().string();
        }
        if (!unlisted.empty())
            return Fail("fixture WAVs have no manifest entry (add cases or remove files): " + unlisted);
    }

    if (dump_writer.enabled) {
        std::filesystem::path parent = std::filesystem::path(dump_path).parent_path();
        if (!parent.empty()) {
            std::error_code ec;
            std::filesystem::create_directories(parent, ec);
            if (ec)
                return Fail("cannot create directory for dump: " + dump_path + " (" + ec.message() + ")");
        }
        dump_writer.out.open(dump_path, std::ios::binary | std::ios::trunc);
        if (!dump_writer.out)
            return Fail("cannot open dump file: " + dump_path);
        ordered_json header;
        header["kind"] = "header";
        header["schema"] = 2;
        header["manifest"] = manifest_path;
        header["fixtures"] = fixtures_dir;
        header["model"] = model_dir;
        if (conf)
            header["modelConf"] = *conf;
        else
            header["modelConf"] = nullptr;
        header["gain"] = manifest["gain"];
        header["libvosk"] = manifest["libvosk"];
        header["grammar"] = manifest["grammar"];
        header["ringCapacity"] = RingBuffer<float>::kCapacity;
        header["decoderRate"] = kDecoderRate;
        if (paced.chunk != 0)
            header["pace"] = paced.chunk;
        dump_writer.Write(header);
    }

    float gain = manifest["gain"].get<float>();

    int rc = vosk_bridge_init(model_dir.c_str(), static_cast<float>(kDecoderRate), gain);
    if (rc != VOSK_BRIDGE_OK)
        return Fail("vosk_bridge_init failed with code " + std::to_string(rc) +
                    " (model: " + model_dir + ")");

    {
        const bool dumping = dump_writer.enabled;
        dump_writer.enabled = false;
        FixtureOutcome check;
        check.file = manifest["cases"][0]["file"].get<std::string>();
        WavData wav;
        std::string error = LoadWav(fixtures_dir + "/" + check.file, wav);
        float g0 = 0.0f, g1 = 0.0f, g2 = 0.0f, g3 = 0.0f;
        uint64_t cold_digest = 0, continued_digest = 0;
        if (error.empty()) {
            rc = vosk_bridge_get_frontend_stats(nullptr, nullptr, nullptr, &g0, nullptr, nullptr);
            if (rc != VOSK_BRIDGE_OK)
                error = "vosk_bridge_get_frontend_stats failed with code " + std::to_string(rc);
        }
        if (error.empty()) {
            rc = vosk_bridge_start_push();
            if (rc != VOSK_BRIDGE_OK)
                error = "vosk_bridge_start_push failed with code " + std::to_string(rc);
        }
        if (error.empty()) {
            if (!PushAll(wav.samples.data(), wav.samples.size(), check))
                error = check.errors.empty() ? "push failed" : check.errors.back();
        }
        if (error.empty())
            error = WaitFrontEnd(wav.samples.size(), nullptr, &cold_digest);
        if (error.empty()) {
            vosk_bridge_stop();
            vosk_bridge_get_frontend_stats(nullptr, nullptr, nullptr, &g1, nullptr, nullptr);
            rc = vosk_bridge_start_push();
            if (rc != VOSK_BRIDGE_OK)
                error = "vosk_bridge_start_push failed with code " + std::to_string(rc);
        }
        if (error.empty()) {
            vosk_bridge_get_frontend_stats(nullptr, nullptr, nullptr, &g2, nullptr, nullptr);
            if (!PushAll(wav.samples.data(), wav.samples.size(), check))
                error = check.errors.empty() ? "push failed" : check.errors.back();
        }
        if (error.empty())
            error = WaitFrontEnd(wav.samples.size(), nullptr, &continued_digest);
        if (error.empty()) {
            vosk_bridge_stop();
            rc = vosk_bridge_reset();
            if (rc != VOSK_BRIDGE_OK)
                error = "vosk_bridge_reset failed with code " + std::to_string(rc);
        }
        if (error.empty()) {
            vosk_bridge_get_frontend_stats(nullptr, nullptr, nullptr, &g3, nullptr, nullptr);
            if (!(g1 != g0 && g2 == g1 && g3 == g0 && continued_digest != cold_digest)) {
                char gains[384];
                std::snprintf(gains, sizeof(gains),
                              "g0 %.9g, g1 %.9g (must differ from g0), g2 %.9g (must equal g1), g3 %.9g (must equal g0), "
                              "cold digest %016llx, continued digest %016llx (must differ from cold)",
                              g0, g1, g2, g3, static_cast<unsigned long long>(cold_digest),
                              static_cast<unsigned long long>(continued_digest));
                error = gains;
            }
        }
        dump_writer.enabled = dumping;
        if (!error.empty()) {
            vosk_bridge_destroy();
            return Fail("front-end lifecycle check failed on '" + check.file + "': " + error);
        }
    }

    {
        std::string grammar = manifest["grammar"].dump();
        rc = vosk_bridge_set_grammar(grammar.c_str());
        if (rc != VOSK_BRIDGE_OK) {
            vosk_bridge_destroy();
            return Fail("vosk_bridge_set_grammar failed with code " + std::to_string(rc));
        }
    }

    const std::vector<float> padding(48000 + RingBuffer<float>::kCapacity, 0.0f);

    std::vector<FixtureOutcome> outcomes;
    bool run_error = false;

    for (auto& c : manifest["cases"]) {
        if (paced.chunk != 0) {
            paced.pushes.clear();
            paced.reads.clear();
        }
        FixtureOutcome r;
        r.file = c.value("file", "");
        if (c.contains("expectedFinals") && c["expectedFinals"].is_array())
            for (auto& e : c["expectedFinals"])
                r.expected.push_back(e.get<std::string>());
        if (c.contains("frontEndSamples"))
            r.expectedFrontEndSamples = c["frontEndSamples"].get<uint64_t>();
        if (c.contains("frontEndDigest"))
            r.expectedFrontEndDigest = c["frontEndDigest"].get<std::string>();

        WavData wav;
        std::string wav_err = LoadWav(fixtures_dir + "/" + r.file, wav);
        if (!wav_err.empty()) {
            r.errors.push_back(wav_err);
            if (dump_writer.enabled)
                DumpCase(r.file, 0, 0);
        } else {
            if (dump_writer.enabled)
                DumpCase(r.file, wav.samples.size(), wav.sample_rate);
            rc = vosk_bridge_start_push();
            if (rc != VOSK_BRIDGE_OK) {
                r.errors.push_back("vosk_bridge_start_push failed with code " +
                                   std::to_string(rc));
            } else {
                if (paced.chunk != 0) {
                    paced.t0 = std::chrono::steady_clock::now();
                    if (PushPaced(wav.samples.data(), wav.samples.size(), r)) {
                        uint64_t fe_samples = 0, fe_digest = 0;
                        std::string wait_err = WaitFrontEndPaced(wav.samples.size(), &fe_samples, &fe_digest, r);
                        if (wait_err.empty()) {
                            r.frontEndSamples = fe_samples;
                            r.frontEndDigest = HexDigest(fe_digest);
                            if (PushPaced(padding.data(), 48000, r) && SpinConsumed(r))
                                PushAll(padding.data() + 48000, padding.size() - 48000, r);
                        } else {
                            r.errors.push_back(wait_err);
                        }
                    }
                } else if (PushAll(wav.samples.data(), wav.samples.size(), r)) {
                    uint64_t fe_samples = 0, fe_digest = 0;
                    std::string wait_err = WaitFrontEnd(wav.samples.size(), &fe_samples, &fe_digest);
                    if (wait_err.empty()) {
                        r.frontEndSamples = fe_samples;
                        r.frontEndDigest = HexDigest(fe_digest);
                        PushAll(padding.data(), padding.size(), r);
                    } else {
                        r.errors.push_back(wait_err);
                    }
                }
                if (dump_writer.enabled) {
                    int stalled = 0;
                    int64_t seen = vosk_bridge_get_consumed();
                    while (seen != static_cast<int64_t>(r.pushed)) {
                        DrainResults(r);
                        std::this_thread::sleep_for(std::chrono::milliseconds(5));
                        int64_t now = vosk_bridge_get_consumed();
                        if (now != seen) {
                            seen = now;
                            stalled = 0;
                        } else if (++stalled > 2000) {
                            r.errors.push_back("drain stalled: consumed " +
                                               std::to_string(now) + " of " +
                                               std::to_string(r.pushed) + " pushed");
                            break;
                        }
                    }
                }
                DrainResults(r);
                vosk_bridge_stop();
                DrainResults(r);
                if (dump_writer.enabled)
                    r.consumed = vosk_bridge_get_consumed();
                vosk_bridge_reset();
            }
        }
        if (dump_writer.enabled)
            DumpCaseEnd(r);

        r.frontEndPass = r.expectedFrontEndSamples && *r.expectedFrontEndSamples == r.frontEndSamples &&
                         r.expectedFrontEndDigest && *r.expectedFrontEndDigest == r.frontEndDigest;
        r.pass = r.errors.empty() && (write_baseline || (r.actual == r.expected && r.frontEndPass));
        if (!r.errors.empty())
            run_error = true;
        if (write_baseline) {
            c["expectedFinals"] = r.actual;
            c["frontEndSamples"] = r.frontEndSamples;
            c["frontEndDigest"] = r.frontEndDigest;
        }
        outcomes.push_back(std::move(r));
    }
    if (write_baseline) {
        manifest["modelConf"] = *conf;
        manifest["_comment"] = kBaselineComment;
    }

    vosk_bridge_destroy();

    if (write_baseline && !run_error) {
        std::ofstream out(manifest_path);
        if (!out)
            return Fail("cannot write manifest: " + manifest_path);
        out << manifest.dump(4) << "\n";
    }

    json report;
    report["cases"] = json::array();
    int passed = 0;
    for (auto& r : outcomes) {
        report["cases"].push_back({{"file", r.file},
                                   {"pass", r.pass},
                                   {"expected", r.expected},
                                   {"actual", r.actual},
                                   {"errors", r.errors},
                                   {"frontEndSamples", r.frontEndSamples},
                                   {"frontEndDigest", r.frontEndDigest},
                                   {"frontEndPass", r.frontEndPass}});
        if (r.pass)
            ++passed;
    }
    if (dump_writer.enabled) {
        ordered_json rec;
        rec["kind"] = "end";
        rec["cases"] = outcomes.size();
        rec["passed"] = passed;
        dump_writer.Write(rec);
        dump_writer.out.close();
        if (!dump_writer.out)
            std::fprintf(stderr, "vosk-bridge-harness: dump write failed: %s\n", dump_path.c_str());
    }
    report["passed"] = passed;
    report["failed"] = static_cast<int>(outcomes.size()) - passed;
    report["total"] = static_cast<int>(outcomes.size());
    report["baselineWritten"] = write_baseline && !run_error;
    std::printf("%s\n", report.dump(2).c_str());

    if (run_error)
        return 2;
    return passed == static_cast<int>(outcomes.size()) ? 0 : 1;
}

int main(int argc, char** argv) {
    // Backstop: anything the up-front validation missed still lands on the
    // legible exit-2 path instead of std::terminate.
    try {
        return RunHarness(argc, argv);
    } catch (const std::exception& e) {
        return Fail(std::string("unhandled error: ") + e.what());
    }
}
