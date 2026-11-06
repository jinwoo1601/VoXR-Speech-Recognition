# ============================================================================
# Purpose:  Build the desktop WAV->transcript harness CLI (Tier C)
# Layer:    NativeBridge.Harness
# Owns:     vosk-bridge-harness (executable target), fe-probe (executable target)
# Depends:  vosk-bridge (shared library target), voxr-frontend-core (static library target), nlohmann/json (vendored)
# ============================================================================

add_executable(vosk-bridge-harness
    main.cpp
    wav_reader.cpp
)

target_include_directories(vosk-bridge-harness PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/../src
    ${CMAKE_CURRENT_SOURCE_DIR}/third_party
)

target_link_libraries(vosk-bridge-harness PRIVATE vosk-bridge)

add_executable(fe-probe
    fe_probe.cpp
    wav_reader.cpp
)

target_include_directories(fe-probe PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/third_party
)

target_link_libraries(fe-probe PRIVATE voxr-frontend-core)
