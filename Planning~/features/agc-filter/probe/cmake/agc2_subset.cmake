# ============================================================================
# Purpose:  Checking and extracting the pinned webrtc-audio-processing and abseil archives, and building the AGC2 subset
# Layer:    Planning.AgcFilter.Probe.CMake
# Owns:     (script, no public types)
# Depends:  (none)
# ============================================================================

get_filename_component(_agc2_default_vendor "${CMAKE_CURRENT_LIST_DIR}/../../../../../NativeBridge~/vendor" ABSOLUTE)
set(AGC2_VENDOR_DIR "${_agc2_default_vendor}" CACHE PATH "Directory holding the pinned AGC2 archives")
set(AGC2_WEBRTC_ARCHIVE "webrtc-audio-processing-2.1.tar.xz" CACHE STRING "webrtc-audio-processing archive file name")
set(AGC2_WEBRTC_SHA256 "ae9302824b2038d394f10213cab05312c564a038434269f11dbf68f511f9f9fe" CACHE STRING "webrtc-audio-processing archive SHA-256")
set(AGC2_ABSL_ARCHIVE "abseil-cpp-20240722.0.tar.gz" CACHE STRING "abseil archive file name")
set(AGC2_ABSL_SHA256 "f50e5ac311a81382da7fa75b97310e4b9006474f9560ac46f54a9967f07d4ae3" CACHE STRING "abseil archive SHA-256")

foreach(_agc2_id IN ITEMS WEBRTC ABSL)
    set(_agc2_file "${AGC2_VENDOR_DIR}/${AGC2_${_agc2_id}_ARCHIVE}")
    if(NOT EXISTS "${_agc2_file}")
        message(FATAL_ERROR "${_agc2_file}: missing - provision NativeBridge~/vendor per NativeBridge~/harness/README.md")
    endif()
    file(SHA256 "${_agc2_file}" _agc2_actual)
    if(NOT _agc2_actual STREQUAL AGC2_${_agc2_id}_SHA256)
        message(FATAL_ERROR "${_agc2_file}: SHA-256 ${_agc2_actual}, pinned ${AGC2_${_agc2_id}_SHA256}")
    endif()
endforeach()

set(AGC2_SRC_ROOT "${CMAKE_BINARY_DIR}/_src")
foreach(_agc2_id IN ITEMS WEBRTC ABSL)
    set(_agc2_stamp "${AGC2_SRC_ROOT}/${AGC2_${_agc2_id}_ARCHIVE}.extracted")
    set(_agc2_done "")
    if(EXISTS "${_agc2_stamp}")
        file(READ "${_agc2_stamp}" _agc2_done)
    endif()
    if(NOT _agc2_done STREQUAL AGC2_${_agc2_id}_SHA256)
        file(ARCHIVE_EXTRACT INPUT "${AGC2_VENDOR_DIR}/${AGC2_${_agc2_id}_ARCHIVE}" DESTINATION "${AGC2_SRC_ROOT}")
        file(WRITE "${_agc2_stamp}" "${AGC2_${_agc2_id}_SHA256}")
    endif()
endforeach()

set(AGC2_WEBRTC_DIR "${AGC2_SRC_ROOT}/webrtc-audio-processing-2.1")
set(AGC2_ABSL_DIR "${AGC2_SRC_ROOT}/abseil-cpp-20240722.0")
foreach(_agc2_dir IN ITEMS "${AGC2_WEBRTC_DIR}" "${AGC2_ABSL_DIR}")
    if(NOT IS_DIRECTORY "${_agc2_dir}")
        message(FATAL_ERROR "${_agc2_dir}: not found after extraction")
    endif()
endforeach()

set(CMAKE_C_VISIBILITY_PRESET hidden)
set(CMAKE_CXX_VISIBILITY_PRESET hidden)
set(CMAKE_VISIBILITY_INLINES_HIDDEN ON)

set(ABSL_PROPAGATE_CXX_STD ON)
set(ABSL_MSVC_STATIC_RUNTIME ON)
set(BUILD_TESTING OFF)
add_subdirectory("${AGC2_ABSL_DIR}" "${CMAKE_BINARY_DIR}/_absl" EXCLUDE_FROM_ALL)

set(_agc2_root "${AGC2_WEBRTC_DIR}/webrtc")

set(AGC2_SOURCES_AGC2
    adaptive_digital_gain_controller.cc
    biquad_filter.cc
    cpu_features.cc
    fixed_digital_level_estimator.cc
    gain_applier.cc
    interpolated_gain_curve.cc
    limiter.cc
    noise_level_estimator.cc
    rnn_vad/auto_correlation.cc
    rnn_vad/features_extraction.cc
    rnn_vad/lp_residual.cc
    rnn_vad/pitch_search.cc
    rnn_vad/pitch_search_internal.cc
    rnn_vad/rnn.cc
    rnn_vad/rnn_fc.cc
    rnn_vad/rnn_gru.cc
    rnn_vad/spectral_features.cc
    rnn_vad/spectral_features_internal.cc
    saturation_protector.cc
    saturation_protector_buffer.cc
    speech_level_estimator.cc
    vad_wrapper.cc
)
list(TRANSFORM AGC2_SOURCES_AGC2 PREPEND "${_agc2_root}/modules/audio_processing/agc2/")

set(AGC2_SOURCES_APM_SUPPORT
    logging/apm_data_dumper.cc
    utility/pffft_wrapper.cc
)
list(TRANSFORM AGC2_SOURCES_APM_SUPPORT PREPEND "${_agc2_root}/modules/audio_processing/")

set(AGC2_SOURCES_COMMON_AUDIO
    audio_util.cc
    resampler/push_resampler.cc
    resampler/push_sinc_resampler.cc
    resampler/sinc_resampler.cc
)
list(TRANSFORM AGC2_SOURCES_COMMON_AUDIO PREPEND "${_agc2_root}/common_audio/")

set(AGC2_SOURCES_RTC_BASE
    checks.cc
    logging.cc
    memory/aligned_malloc.cc
    platform_thread_types.cc
    string_encode.cc
    string_utils.cc
    strings/string_builder.cc
    system_time.cc
    time_utils.cc
)
list(TRANSFORM AGC2_SOURCES_RTC_BASE PREPEND "${_agc2_root}/rtc_base/")

set(AGC2_SOURCES_SYSTEM_WRAPPERS
    source/cpu_features.cc
    source/metrics.cc
)
list(TRANSFORM AGC2_SOURCES_SYSTEM_WRAPPERS PREPEND "${_agc2_root}/system_wrappers/")

set(AGC2_SOURCES_THIRD_PARTY
    "${_agc2_root}/third_party/pffft/src/pffft.c"
    "${_agc2_root}/third_party/rnnoise/src/rnn_vad_weights.cc"
)

set(AGC2_SOURCES_X86_SSE2
    "${_agc2_root}/common_audio/resampler/sinc_resampler_sse.cc"
)
set(AGC2_SOURCES_X86_AVX2
    "${_agc2_root}/common_audio/resampler/sinc_resampler_avx2.cc"
    "${_agc2_root}/modules/audio_processing/agc2/rnn_vad/vector_math_avx2.cc"
)
set(AGC2_SOURCES_NEON
    "${_agc2_root}/common_audio/resampler/sinc_resampler_neon.cc"
)

set(AGC2_DEFINES
    WEBRTC_LIBRARY_IMPL
    _WINSOCKAPI_
    NDEBUG
    WEBRTC_APM_DEBUG_DUMP=0
)

set(AGC2_SOURCES
    ${AGC2_SOURCES_AGC2}
    ${AGC2_SOURCES_APM_SUPPORT}
    ${AGC2_SOURCES_COMMON_AUDIO}
    ${AGC2_SOURCES_RTC_BASE}
    ${AGC2_SOURCES_SYSTEM_WRAPPERS}
    ${AGC2_SOURCES_THIRD_PARTY}
)

if(ANDROID)
    list(APPEND AGC2_DEFINES WEBRTC_ANDROID WEBRTC_LINUX WEBRTC_POSIX)
    if(ANDROID_ABI STREQUAL "arm64-v8a")
        list(APPEND AGC2_DEFINES WEBRTC_ARCH_ARM64 WEBRTC_HAS_NEON)
        list(APPEND AGC2_SOURCES ${AGC2_SOURCES_NEON})
    endif()
elseif(MSVC)
    list(APPEND AGC2_DEFINES WEBRTC_WIN _WIN32 __STDC_FORMAT_MACROS=1 NOMINMAX _USE_MATH_DEFINES WEBRTC_ENABLE_AVX2)
    list(APPEND AGC2_SOURCES ${AGC2_SOURCES_X86_SSE2} ${AGC2_SOURCES_X86_AVX2})
    set_source_files_properties(${AGC2_SOURCES_X86_AVX2} PROPERTIES COMPILE_OPTIONS "/arch:AVX2")
endif()

set_source_files_properties("${_agc2_root}/third_party/pffft/src/pffft.c" PROPERTIES COMPILE_DEFINITIONS _GNU_SOURCE)

add_library(webrtc_agc2 STATIC ${AGC2_SOURCES})
target_compile_definitions(webrtc_agc2 PUBLIC ${AGC2_DEFINES})
target_include_directories(webrtc_agc2 PUBLIC "${_agc2_root}")
target_link_libraries(webrtc_agc2 PUBLIC absl::strings)
set_target_properties(webrtc_agc2 PROPERTIES
    POSITION_INDEPENDENT_CODE ON
    C_STANDARD 11
)
if(ANDROID)
    target_link_libraries(webrtc_agc2 PUBLIC log)
elseif(MSVC)
    target_link_libraries(webrtc_agc2 PUBLIC winmm)
endif()
