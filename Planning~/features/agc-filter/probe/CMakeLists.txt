# ============================================================================
# Purpose:  AGC2 subset probe: arm64 bridge variants and the Windows DLL and driver
# Layer:    Planning.AgcFilter.Probe
# Owns:     (script, no public types)
# Depends:  agc2_subset.cmake, webrtc_agc2 (static library target)
# ============================================================================

cmake_minimum_required(VERSION 3.18)
project(agc2-probe LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_MSVC_RUNTIME_LIBRARY MultiThreaded)

get_filename_component(PROBE_BRIDGE_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../../../../NativeBridge~" ABSOLUTE)
get_filename_component(PROBE_PLUGIN_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../../../../Runtime/Plugins/Android/arm64-v8a" ABSOLUTE)

include(${CMAKE_CURRENT_LIST_DIR}/cmake/agc2_subset.cmake)

set(PROBE_CHAIN_SOURCES
    ${CMAKE_CURRENT_SOURCE_DIR}/src/agc2_chain.cpp
)

if(ANDROID)
    add_library(vosk SHARED IMPORTED)
    set_target_properties(vosk PROPERTIES IMPORTED_LOCATION "${PROBE_PLUGIN_DIR}/libvosk.so")

    set(PROBE_BRIDGE_SOURCES
        ${PROBE_BRIDGE_DIR}/src/vosk_bridge.cpp
        ${PROBE_BRIDGE_DIR}/src/audio_capture_audiorecord.cpp
    )

    foreach(_probe_variant IN ITEMS bridge-today bridge-bare bridge-with)
        add_library(${_probe_variant} SHARED ${PROBE_BRIDGE_SOURCES})
        target_compile_definitions(${_probe_variant} PRIVATE VOSK_BRIDGE_CAPTURE_AUDIORECORD)
        target_include_directories(${_probe_variant} PRIVATE
            ${PROBE_BRIDGE_DIR}/src
            ${PROBE_BRIDGE_DIR}/include
        )
        target_link_libraries(${_probe_variant} PRIVATE vosk log)
        set_target_properties(${_probe_variant} PROPERTIES
            C_VISIBILITY_PRESET hidden
            CXX_VISIBILITY_PRESET hidden
        )
        target_compile_options(${_probe_variant} PRIVATE $<$<CONFIG:Release>:-flto=thin>)
        target_link_options(${_probe_variant} PRIVATE $<$<CONFIG:Release>:-flto=thin>)
    endforeach()

    set_target_properties(bridge-today PROPERTIES LINK_FLAGS "-Wl,--no-undefined -Wl,--strip-all")
    set_target_properties(bridge-bare bridge-with PROPERTIES
        LINK_FLAGS "-Wl,--no-undefined -Wl,--strip-all -Wl,--exclude-libs,ALL")

    target_sources(bridge-with PRIVATE ${PROBE_CHAIN_SOURCES} ${CMAKE_CURRENT_SOURCE_DIR}/src/keep_alive.cpp)
    target_include_directories(bridge-with PRIVATE ${CMAKE_CURRENT_SOURCE_DIR}/src)
    target_link_libraries(bridge-with PRIVATE webrtc_agc2)
elseif(MSVC)
    set(CMAKE_RUNTIME_OUTPUT_DIRECTORY "$<1:${CMAKE_BINARY_DIR}/bin>")

    add_library(probe-frontend SHARED ${PROBE_CHAIN_SOURCES} ${CMAKE_CURRENT_SOURCE_DIR}/src/probe_dll.cpp)
    target_include_directories(probe-frontend PRIVATE ${CMAKE_CURRENT_SOURCE_DIR}/src)
    target_link_libraries(probe-frontend PRIVATE webrtc_agc2)

    add_executable(agc2-probe-driver
        ${CMAKE_CURRENT_SOURCE_DIR}/src/driver_main.cpp
        ${PROBE_BRIDGE_DIR}/harness/wav_reader.cpp
    )
    target_include_directories(agc2-probe-driver PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}/src
        ${PROBE_BRIDGE_DIR}/src
        ${PROBE_BRIDGE_DIR}/harness
    )
    target_link_libraries(agc2-probe-driver PRIVATE probe-frontend)
endif()
