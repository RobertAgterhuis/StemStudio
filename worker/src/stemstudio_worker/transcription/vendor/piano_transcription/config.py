# Vendored from qiuqiangkong/piano_transcription_inference 0.0.6 (MIT License,
# Copyright Qiuqiang Kong, see LICENSE): piano_transcription_inference/config.py
# Unchanged.
# See LICENSE and NOTICE in this directory.

sample_rate = 16000
classes_num = 88    # Number of notes of piano
begin_note = 21     # MIDI note of A0, the lowest note of a piano.
segment_seconds = 10.	# Training segment duration
hop_seconds = 1.
frames_per_second = 100
velocity_scale = 128
