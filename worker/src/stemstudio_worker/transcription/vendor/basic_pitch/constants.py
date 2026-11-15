# Vendored from spotify/basic-pitch v0.4.0 (Apache License 2.0, Copyright 2022 Spotify AB):
# basic_pitch/constants.py - the window and frame constants only.
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

FFT_HOP = 256
AUDIO_SAMPLE_RATE = 22050
AUDIO_WINDOW_LENGTH = 2  # duration in seconds of training examples - original 1
ANNOTATIONS_FPS = AUDIO_SAMPLE_RATE // FFT_HOP
ANNOT_N_FRAMES = ANNOTATIONS_FPS * AUDIO_WINDOW_LENGTH
AUDIO_N_SAMPLES = AUDIO_SAMPLE_RATE * AUDIO_WINDOW_LENGTH - FFT_HOP
MIDI_OFFSET = 21
MAX_FREQ_IDX = 87
