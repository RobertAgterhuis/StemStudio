/*
 * stemstudio_soundtouch.cpp - WASM ABI version 1 of the StemStudio SoundTouch module over
 * soundtouch::SoundTouch (SoundTouch 2.4.1, unmodified upstream source).
 *
 * Binding description: specs/012-practice-tempo/contracts/audio-engine.md, section "WASM ABI";
 * declarations: third_party/soundtouch/include/stemstudio_soundtouch.h.
 *
 * Copyright (C) 2026 StemStudio contributors.
 *
 * This library is free software; you can redistribute it and/or modify it under the terms of the
 * GNU Lesser General Public License as published by the Free Software Foundation; either version
 * 2.1 of the License, or (at your option) any later version. It is compiled into soundtouch.wasm
 * together with SoundTouch and is distributed under the same licence (COPYING.TXT).
 *
 * This library is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
 * without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Lesser General Public License for more details.
 *
 * Handles and pointers are 32-bit offsets into the module's linear memory (wasm32). Built without
 * C++ exceptions: allocation uses std::nothrow and arguments are checked before they reach
 * SoundTouch, so no call can abort the module.
 */
#include "stemstudio_soundtouch.h"

#include <SoundTouch.h>

#include <cstdint>
#include <cstdlib>
#include <new>

namespace {

using soundtouch::SoundTouch;

/** Linear-memory offset -> instance (0 -> nullptr). */
SoundTouch *instance(int32_t handle) {
    return reinterpret_cast<SoundTouch *>(static_cast<uintptr_t>(handle));
}

/** Linear-memory offset -> float samples. */
float *samples_at(int32_t pointer) {
    return reinterpret_cast<float *>(static_cast<uintptr_t>(pointer));
}

/** Pointer -> 32-bit linear-memory offset. */
int32_t offset_of(const void *pointer) {
    return static_cast<int32_t>(reinterpret_cast<uintptr_t>(pointer));
}

}  // namespace

extern "C" {

int32_t st_abi_version(void) {
    return STEMSTUDIO_SOUNDTOUCH_ABI_VERSION;
}

int32_t st_create(int32_t sample_rate, int32_t channels) {
    if (sample_rate <= 0 || channels < 1 || channels > 2) {
        return 0;
    }
    SoundTouch *created = new (std::nothrow) SoundTouch();
    if (created == nullptr) {
        return 0;
    }
    created->setSampleRate(static_cast<unsigned int>(sample_rate));
    created->setChannels(static_cast<unsigned int>(channels));
    created->setTempo(1.0);
    created->setRate(1.0);
    created->setPitch(1.0);
    return offset_of(created);
}

void st_destroy(int32_t handle) {
    delete instance(handle);
}

void st_set_pitch(int32_t handle, double pitch) {
    SoundTouch *target = instance(handle);
    if (target != nullptr && pitch > 0.0) {
        target->setPitch(pitch);
    }
}

int32_t st_set_setting(int32_t handle, int32_t id, int32_t value) {
    SoundTouch *target = instance(handle);
    if (target == nullptr) {
        return 0;
    }
    return target->setSetting(id, value) ? 1 : 0;
}

int32_t st_initial_latency(int32_t handle) {
    SoundTouch *target = instance(handle);
    return target == nullptr ? 0 : target->getSetting(SETTING_INITIAL_LATENCY);
}

void st_put(int32_t handle, int32_t samples, int32_t frames) {
    SoundTouch *target = instance(handle);
    if (target != nullptr && samples != 0 && frames > 0) {
        target->putSamples(samples_at(samples), static_cast<unsigned int>(frames));
    }
}

int32_t st_receive(int32_t handle, int32_t samples, int32_t max_frames) {
    SoundTouch *target = instance(handle);
    if (target == nullptr || samples == 0 || max_frames <= 0) {
        return 0;
    }
    unsigned int received = target->receiveSamples(samples_at(samples), static_cast<unsigned int>(max_frames));
    return static_cast<int32_t>(received);
}

void st_clear(int32_t handle) {
    SoundTouch *target = instance(handle);
    if (target != nullptr) {
        target->clear();
    }
}

int32_t st_malloc(int32_t bytes) {
    if (bytes <= 0) {
        return 0;
    }
    size_t rounded = (static_cast<size_t>(bytes) + 15u) & ~static_cast<size_t>(15u);
    return offset_of(std::aligned_alloc(16, rounded));
}

void st_free(int32_t pointer) {
    std::free(reinterpret_cast<void *>(static_cast<uintptr_t>(pointer)));
}

}  // extern "C"
