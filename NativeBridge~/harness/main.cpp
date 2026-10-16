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

struct FixtureOutcome {
    std::string file;
    std::vector<std::string> expected;
    std::vector<std::string> actual;
    std::vector<std::string> errors;
    bool pass = false;
    size_t pushed = 0;     // input samples pushed for this case so far
    size_t prevPushed = 0; // `pushed` at the previous drain
    size_t seq = 0;        // results popped for this case so far
    int64_t consumed = 0;  // --dump: input samples the bridge consumed, at case end
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
    dump_writer.Write(rec);
}

// Pops everything currently queued. Result strings are copied immediately —
// the pointer from vosk_bridge_get_result dies on the next call. Non-final
// {"error": ...} entries (ring overflow, device error) fail the run loudly.
void DrainResults(FixtureOutcome& r) {
    int is_final = 0, length = 0;
    const char* p = nullptr;
    while ((p = vosk_bridge_get_result(&is_final, &length)) != nullptr) {
        std::string payload(p, static_cast<size_t>(length));
        // Dumped before handling, re-parsed as ordered_json so Vosk's own key
        // order survives; every result of one drain carries that drain's pair,
        // and its own bridge position (consumed, origin).
        if (dump_writer.enabled) {
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

int Fail(const std::string& msg) {
    std::fprintf(stderr, "vosk-bridge-harness: %s\n", msg.c_str());
    return 2;
}

} // namespace

int RunHarness(int argc, char** argv) {
    std::string model_dir, fixtures_dir, manifest_path, dump_path;
    bool write_baseline = false;

    for (int i = 1; i < argc; ++i) {
        std::string arg = argv[i];
        if (arg == "--model" && i + 1 < argc) model_dir = argv[++i];
        else if (arg == "--fixtures" && i + 1 < argc) fixtures_dir = argv[++i];
        else if (arg == "--manifest" && i + 1 < argc) manifest_path = argv[++i];
        else if (arg == "--write-baseline") write_baseline = true;
        else if (arg == "--dump" && i + 1 < argc) { dump_path = argv[++i]; dump_writer.enabled = true; }
        else return Fail("unknown or incomplete argument: " + arg + "\nusage: vosk-bridge-harness --model <dir> --fixtures <dir> --manifest <expectations.json> [--write-baseline] [--dump <file>]");
    }
    if (model_dir.empty() || fixtures_dir.empty() || manifest_path.empty())
        return Fail("--model, --fixtures and --manifest are all required");

    // The decoded model's conf, read as exact bytes (the pin compares them);
    // a conf that cannot be opened is absent.
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

    // Schema validation up front: a hand-edited manifest must fail legibly
    // (exit 2), never abort mid-run — and the decode-regime pins are REQUIRED,
    // because a baseline decoded without them is meaningless (a grammar-less
    // run would free-decode and silently invalidate every expectation).
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
    // The conf pin: decode only at the pinned conf; only --write-baseline
    // writes it, and it has no null form.
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
    }

    // Corpus-drift guard: every WAV in the fixtures dir must be listed, or a
    // committed fixture could silently go unverified forever (mirror of Tier
    // B's EveryCommittedWav_IsListedInTheManifest).
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

    // Opt-in dump: opened before the model loads, so a bad path fails legibly
    // (exit 2) up front; the header pins the decode regime the dump came from.
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
        dump_writer.Write(header);
    }

    float gain = manifest["gain"].get<float>();

    int rc = vosk_bridge_init(model_dir.c_str(), static_cast<float>(kDecoderRate), gain);
    if (rc != VOSK_BRIDGE_OK)
        return Fail("vosk_bridge_init failed with code " + std::to_string(rc) +
                    " (model: " + model_dir + ")");

    {
        std::string grammar = manifest["grammar"].dump();
        rc = vosk_bridge_set_grammar(grammar.c_str());
        if (rc != VOSK_BRIDGE_OK) {
            vosk_bridge_destroy();
            return Fail("vosk_bridge_set_grammar failed with code " + std::to_string(rc));
        }
    }

    // Padding pushed after each fixture: 1 s of silence lets endpointing fire
    // in sample time, and a further ring-capacity's worth guarantees every
    // fixture sample was consumed before stop (consumed >= pushed - capacity).
    const std::vector<float> padding(48000 + RingBuffer<float>::kCapacity, 0.0f);

    std::vector<FixtureOutcome> outcomes;
    bool run_error = false;

    for (auto& c : manifest["cases"]) {
        FixtureOutcome r;
        r.file = c.value("file", "");
        if (c.contains("expectedFinals") && c["expectedFinals"].is_array())
            for (auto& e : c["expectedFinals"])
                r.expected.push_back(e.get<std::string>());

        WavData wav;
        std::string wav_err = LoadWav(fixtures_dir + "/" + r.file, wav);
        if (!wav_err.empty()) {
            r.errors.push_back(wav_err);
            if (dump_writer.enabled)
                DumpCase(r.file, 0, 0); // the reader still sees every case
        } else {
            if (dump_writer.enabled)
                DumpCase(r.file, wav.samples.size(), wav.sample_rate);
            rc = vosk_bridge_start_push();
            if (rc != VOSK_BRIDGE_OK) {
                r.errors.push_back("vosk_bridge_start_push failed with code " +
                                   std::to_string(rc));
            } else {
                if (PushAll(wav.samples.data(), wav.samples.size(), r))
                    PushAll(padding.data(), padding.size(), r);
                // --dump only: drain until the bridge has consumed every pushed
                // sample, so stop ends each case at the same point in every run
                // and every later origin depends on the corpus alone. Same stall
                // rule as PushAll: ~10 s without progress is a case error.
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
                        } else if (++stalled > 2000) { // ~10 s without progress
                            r.errors.push_back("drain stalled: consumed " +
                                               std::to_string(now) + " of " +
                                               std::to_string(r.pushed) + " pushed");
                            break;
                        }
                    }
                }
                DrainResults(r);
                vosk_bridge_stop();
                DrainResults(r); // the tail final is flushed during stop's join
                if (dump_writer.enabled)
                    r.consumed = vosk_bridge_get_consumed(); // held after stop
                vosk_bridge_reset();
            }
        }
        if (dump_writer.enabled)
            DumpCaseEnd(r); // every case's frame closes here, on every branch

        r.pass = r.errors.empty() && (write_baseline || r.actual == r.expected);
        if (!r.errors.empty())
            run_error = true;
        if (write_baseline)
            c["expectedFinals"] = r.actual;
        outcomes.push_back(std::move(r));
    }
    if (write_baseline)
        manifest["modelConf"] = *conf; // present: validation refused an absent conf

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
                                   {"errors", r.errors}});
        if (r.pass)
            ++passed;
    }
    // The trailer proves the dump complete. A bad stream is reported, never a
    // verdict: the exit code stays the run's.
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
