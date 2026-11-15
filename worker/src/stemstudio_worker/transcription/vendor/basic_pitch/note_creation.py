# Vendored from spotify/basic-pitch v0.4.0 (Apache License 2.0, Copyright 2022 Spotify AB):
# basic_pitch/note_creation.py (get_infered_onsets, model_frames_to_time,
# output_to_notes_polyphonic) and basic_pitch/inference.py (window_audio_file, unwrap_output).
# Modified for StemStudio: no librosa/resampy/pretty_midi/mir_eval/TensorFlow imports, frame
# times computed directly, constrain_frequency removed, windowing on an in-memory array.
# See LICENSE and NOTICE in this directory.
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

from typing import Iterable, List, Optional, Tuple

import numpy as np
import scipy.signal

from .constants import (
    ANNOT_N_FRAMES,
    ANNOTATIONS_FPS,
    AUDIO_N_SAMPLES,
    AUDIO_SAMPLE_RATE,
    FFT_HOP,
    MAX_FREQ_IDX,
    MIDI_OFFSET,
)


def window_audio_file(audio_original: np.ndarray, hop_size: int) -> Iterable[np.ndarray]:
    """
    Pad appropriately an audio file, and return as
    windowed signal, with window length = AUDIO_N_SAMPLES
    """
    for i in range(0, audio_original.shape[0], hop_size):
        window = audio_original[i : i + AUDIO_N_SAMPLES]
        if len(window) < AUDIO_N_SAMPLES:
            window = np.pad(
                window,
                pad_width=[[0, AUDIO_N_SAMPLES - len(window)]],
            )
        yield np.expand_dims(window, axis=-1)


def unwrap_output(
    output: np.ndarray,
    audio_original_length: int,
    n_overlapping_frames: int,
) -> Optional[np.ndarray]:
    """Unwrap batched model predictions to a single matrix.

    Args:
        output: array (n_batches, n_times_short, n_freqs)
        audio_original_length: length of original audio signal (in samples)
        n_overlapping_frames: number of overlapping frames in the output

    Returns:
        array (n_times, n_freqs)
    """
    if len(output.shape) != 3:
        return None

    n_olap = int(0.5 * n_overlapping_frames)
    if n_olap > 0:
        # remove half of the overlapping frames from beginning and end
        output = output[:, n_olap:-n_olap, :]

    output_shape = output.shape
    n_output_frames_original = int(np.floor(audio_original_length * (ANNOTATIONS_FPS / AUDIO_SAMPLE_RATE)))
    unwrapped_output = output.reshape(output_shape[0] * output_shape[1], output_shape[2])
    return unwrapped_output[:n_output_frames_original, :]  # trim to original audio length


def get_infered_onsets(onsets: np.ndarray, frames: np.ndarray, n_diff: int = 2) -> np.ndarray:
    """Infer onsets from large changes in frame amplitudes.

    Args:
        onsets: Array of note onset predictions.
        frames: Audio frames.
        n_diff: Differences used to detect onsets.

    Returns:
        The maximum between the predicted onsets and its differences.
    """
    diffs = []
    for n in range(1, n_diff + 1):
        frames_appended = np.concatenate([np.zeros((n, frames.shape[1])), frames])
        diffs.append(frames_appended[n:, :] - frames_appended[:-n, :])
    frame_diff = np.min(diffs, axis=0)
    frame_diff[frame_diff < 0] = 0
    frame_diff[:n_diff, :] = 0
    frame_diff = np.max(onsets) * frame_diff / np.max(frame_diff)  # rescale to have the same max as onsets

    max_onsets_diff = np.max([onsets, frame_diff], axis=0)  # use the max of the predicted onsets and the differences

    return max_onsets_diff


def model_frames_to_time(n_frames: int) -> np.ndarray:
    original_times = np.arange(n_frames) * FFT_HOP / AUDIO_SAMPLE_RATE
    window_numbers = np.floor(np.arange(n_frames) / ANNOT_N_FRAMES)
    window_offset = (FFT_HOP / AUDIO_SAMPLE_RATE) * (
        ANNOT_N_FRAMES - (AUDIO_N_SAMPLES / FFT_HOP)
    ) + 0.0018  # this is a magic number, but it's needed for this to align properly
    times = original_times - (window_offset * window_numbers)
    return times


def output_to_notes_polyphonic(
    frames: np.ndarray,
    onsets: np.ndarray,
    onset_thresh: float,
    frame_thresh: float,
    min_note_len: int,
    infer_onsets: bool,
    melodia_trick: bool = True,
    energy_tol: int = 11,
) -> List[Tuple[int, int, int, float]]:
    """Decode raw model output to polyphonic note events

    Args:
        frames: Frame activation matrix (n_times, n_freqs).
        onsets: Onset activation matrix (n_times, n_freqs).
        onset_thresh: Minimum amplitude of an onset activation to be considered an onset.
        frame_thresh: Minimum amplitude of a frame activation for a note to remain "on".
        min_note_len: Minimum allowed note length in frames.
        infer_onsets: If True, add additional onsets when there are large differences in frame amplitudes.
        melodia_trick : Whether to use the melodia trick to better detect notes.
        energy_tol: Drop notes below this energy.

    Returns:
        list of tuples [(start_time_frames, end_time_frames, pitch_midi, amplitude)]
        representing the note events, where amplitude is a number between 0 and 1
    """

    n_frames = frames.shape[0]

    # use onsets inferred from frames in addition to the predicted onsets
    if infer_onsets:
        onsets = get_infered_onsets(onsets, frames)

    peak_thresh_mat = np.zeros(onsets.shape)
    peaks = scipy.signal.argrelmax(onsets, axis=0)
    peak_thresh_mat[peaks] = onsets[peaks]

    onset_idx = np.where(peak_thresh_mat >= onset_thresh)
    onset_time_idx = onset_idx[0][::-1]  # sort to go backwards in time
    onset_freq_idx = onset_idx[1][::-1]  # sort to go backwards in time

    remaining_energy = np.zeros(frames.shape)
    remaining_energy[:, :] = frames[:, :]

    # loop over onsets
    note_events = []
    for note_start_idx, freq_idx in zip(onset_time_idx, onset_freq_idx):
        # if we're too close to the end of the audio, continue
        if note_start_idx >= n_frames - 1:
            continue

        # find time index at this frequency band where the frames drop below an energy threshold
        i = note_start_idx + 1
        k = 0  # number of frames since energy dropped below threshold
        while i < n_frames - 1 and k < energy_tol:
            if remaining_energy[i, freq_idx] < frame_thresh:
                k += 1
            else:
                k = 0
            i += 1

        i -= k  # go back to frame above threshold

        # if the note is too short, skip it
        if i - note_start_idx <= min_note_len:
            continue

        remaining_energy[note_start_idx:i, freq_idx] = 0
        if freq_idx < MAX_FREQ_IDX:
            remaining_energy[note_start_idx:i, freq_idx + 1] = 0
        if freq_idx > 0:
            remaining_energy[note_start_idx:i, freq_idx - 1] = 0

        # add the note
        amplitude = np.mean(frames[note_start_idx:i, freq_idx])
        note_events.append(
            (
                note_start_idx,
                i,
                freq_idx + MIDI_OFFSET,
                amplitude,
            )
        )

    if melodia_trick:
        energy_shape = remaining_energy.shape

        while np.max(remaining_energy) > frame_thresh:
            i_mid, freq_idx = np.unravel_index(np.argmax(remaining_energy), energy_shape)
            remaining_energy[i_mid, freq_idx] = 0

            # forward pass
            i = i_mid + 1
            k = 0
            while i < n_frames - 1 and k < energy_tol:
                if remaining_energy[i, freq_idx] < frame_thresh:
                    k += 1
                else:
                    k = 0

                remaining_energy[i, freq_idx] = 0
                if freq_idx < MAX_FREQ_IDX:
                    remaining_energy[i, freq_idx + 1] = 0
                if freq_idx > 0:
                    remaining_energy[i, freq_idx - 1] = 0

                i += 1

            i_end = i - 1 - k  # go back to frame above threshold

            # backward pass
            i = i_mid - 1
            k = 0
            while i > 0 and k < energy_tol:
                if remaining_energy[i, freq_idx] < frame_thresh:
                    k += 1
                else:
                    k = 0

                remaining_energy[i, freq_idx] = 0
                if freq_idx < MAX_FREQ_IDX:
                    remaining_energy[i, freq_idx + 1] = 0
                if freq_idx > 0:
                    remaining_energy[i, freq_idx - 1] = 0

                i -= 1

            i_start = i + 1 + k  # go back to frame above threshold
            assert i_start >= 0, "{}".format(i_start)
            assert i_end < n_frames

            if i_end - i_start <= min_note_len:
                # note is too short, skip it
                continue

            # add the note
            amplitude = np.mean(frames[i_start:i_end, freq_idx])
            note_events.append(
                (
                    i_start,
                    i_end,
                    freq_idx + MIDI_OFFSET,
                    amplitude,
                )
            )

    return note_events
