# Vendored from qiuqiangkong/piano_transcription_inference 0.0.6 (MIT License,
# Copyright Qiuqiang Kong, see LICENSE): piano_transcription_inference/inference.py
# Modified for StemStudio: PianoTranscription.enframe/deframe as module functions; the class (shell download, prints) is not vendored.
# See LICENSE and NOTICE in this directory.

import numpy as np


def enframe(x, segment_samples):
    """Enframe long sequence to short segments.

    Args:
      x: (1, audio_samples)
      segment_samples: int

    Returns:
      batch: (N, segment_samples)
    """
    assert x.shape[1] % segment_samples == 0
    batch = []

    pointer = 0
    while pointer + segment_samples <= x.shape[1]:
        batch.append(x[:, pointer : pointer + segment_samples])
        pointer += segment_samples // 2

    batch = np.concatenate(batch, axis=0)
    return batch

def deframe(x):
    """Deframe predicted segments to original sequence.

    Args:
      x: (N, segment_frames, classes_num)

    Returns:
      y: (audio_frames, classes_num)
    """
    if x.shape[0] == 1:
        return x[0]

    else:
        x = x[:, 0 : -1, :]
        """Remove an extra frame in the end of each segment caused by the
        'center=True' argument when calculating spectrogram."""
        (N, segment_samples, classes_num) = x.shape
        assert segment_samples % 4 == 0

        y = []
        y.append(x[0, 0 : int(segment_samples * 0.75)])
        for i in range(1, N - 1):
            y.append(x[i, int(segment_samples * 0.25) : int(segment_samples * 0.75)])
        y.append(x[-1, int(segment_samples * 0.25) :])
        y = np.concatenate(y, axis=0)
        return y
