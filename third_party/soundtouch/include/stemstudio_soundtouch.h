/*
 * stemstudio_soundtouch.h - WASM ABI version 1 of the StemStudio SoundTouch module.
 *
 * Binding description: specs/012-practice-tempo/contracts/audio-engine.md, section "WASM ABI".
 * The module soundtouch.wasm is SoundTouch 2.4.1 (LGPL-2.1, unmodified upstream source) plus the
 * shim third_party/soundtouch/shim/stemstudio_soundtouch.cpp that implements these functions.
 * Any build of (a modified) SoundTouch that exports exactly this ABI can replace the shipped file
 * (LGPL-2.1 section 6; see third_party/soundtouch/README.md).
 *
 * This header is part of the replaceable library interface and is licensed LGPL-2.1-or-later,
 * like the shim that implements it.
 *
 * Conventions:
 *   - All pointers and handles are 32-bit offsets into the exported linear memory (wasm32).
 *   - Samples are 32-bit float, interleaved, `channels` values per frame.
 *   - A handle is created with st_create and released with st_destroy; 0 means failure.
 *   - Reactor module: when the module exports `_initialize`, the host calls it once, before any
 *     other export (Emscripten STANDALONE_WASM --no-entry runs static constructors there).
 *   - No function allocates except st_create and st_malloc; the host allocates its sample
 *     buffers once with st_malloc and reuses them (no allocation per render quantum).
 */
#ifndef STEMSTUDIO_SOUNDTOUCH_H
#define STEMSTUDIO_SOUNDTOUCH_H

#include <stdint.h>

#if defined(__EMSCRIPTEN__)
#include <emscripten/emscripten.h>
#define ST_EXPORT EMSCRIPTEN_KEEPALIVE
#else
#define ST_EXPORT
#endif

/** ABI version implemented by this header; st_abi_version() returns it. */
#define STEMSTUDIO_SOUNDTOUCH_ABI_VERSION 1

#ifdef __cplusplus
extern "C" {
#endif

/** Returns STEMSTUDIO_SOUNDTOUCH_ABI_VERSION (1). */
ST_EXPORT int32_t st_abi_version(void);

/**
 * Creates one SoundTouch instance with tempo 1, rate 1 and pitch 1.
 * @param sample_rate frames per second (e.g. 44100 or 48000), > 0
 * @param channels    1 or 2
 * @return handle, or 0 on invalid arguments or out of memory
 */
ST_EXPORT int32_t st_create(int32_t sample_rate, int32_t channels);

/** Destroys an instance created by st_create; 0 is ignored. */
ST_EXPORT void st_destroy(int32_t handle);

/** SoundTouch::setPitch(pitch); pitch factor > 0 (product range 0.42..4.0). */
ST_EXPORT void st_set_pitch(int32_t handle, double pitch);

/**
 * SoundTouch::setSetting(id, value).
 * @return 1 when the setting was accepted, 0 otherwise
 */
ST_EXPORT int32_t st_set_setting(int32_t handle, int32_t id, int32_t value);

/** SoundTouch::getSetting(SETTING_INITIAL_LATENCY): initial latency in frames for the current settings. */
ST_EXPORT int32_t st_initial_latency(int32_t handle);

/** SoundTouch::putSamples: `frames` interleaved frames starting at `samples`. */
ST_EXPORT void st_put(int32_t handle, int32_t samples, int32_t frames);

/**
 * SoundTouch::receiveSamples: copies at most `max_frames` interleaved frames to `samples`.
 * @return number of frames written (0..max_frames)
 */
ST_EXPORT int32_t st_receive(int32_t handle, int32_t samples, int32_t max_frames);

/** SoundTouch::clear(): drops all buffered input and output. */
ST_EXPORT void st_clear(int32_t handle);

/** Allocates `bytes` bytes in linear memory (16-byte aligned); 0 on failure. */
ST_EXPORT int32_t st_malloc(int32_t bytes);

/** Frees a pointer returned by st_malloc; 0 is ignored. */
ST_EXPORT void st_free(int32_t pointer);

#ifdef __cplusplus
}
#endif

#endif /* STEMSTUDIO_SOUNDTOUCH_H */
