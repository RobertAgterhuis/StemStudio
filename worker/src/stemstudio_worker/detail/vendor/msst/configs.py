# Hyper-parameters of the two MSST checkpoints used by StemStudio, transcribed from their YAML
# configuration files (research.md D-07) so that no config loader (omegaconf, ml_collections)
# is needed at runtime:
#   config_drumsep_mdx23c.yaml                 SHA-256 17d1649a227f841165bdb4c11a42082898192a1ea3ceab7e7e0b9293d6589dd6
#   mvsep_mega_model_bs_roformer_53_stems.yaml SHA-256 7e198062a251587088adb91215a4f44ab59e67bd62fcc805cf54d6e7dfc51103
# Only the inference-relevant keys are kept. Part of the vendored MSST code (see NOTICE).

from types import SimpleNamespace

DRUMSEP_INSTRUMENTS = ("kick", "snare", "toms", "hh", "ride", "crash")

MDX23C_DRUMSEP = SimpleNamespace(
    audio=SimpleNamespace(
        chunk_size=130560,
        dim_f=1024,
        dim_t=256,
        hop_length=512,
        n_fft=2048,
        num_channels=2,
        sample_rate=44100,
        min_mean_abs=0.001,
    ),
    model=SimpleNamespace(
        act="gelu",
        bottleneck_factor=4,
        growth=128,
        norm="InstanceNorm",
        num_blocks_per_scale=2,
        num_channels=128,
        num_scales=5,
        num_subbands=4,
        scale=[2, 2],
    ),
    training=SimpleNamespace(instruments=list(DRUMSEP_INSTRUMENTS), target_instrument=None),
)

MEGA_INSTRUMENTS = (
    "accordion", "acoustic-guitar", "back-vocal", "banjo", "bass", "bassoon", "bells",
    "bowed_strings", "brass", "cello", "clarinet", "congas", "digital-piano", "dobro",
    "double-bass", "drums", "electric-guitar", "flute", "french-horn", "glockenspiel", "guitar",
    "harmonica", "harp", "harpsichord", "hh", "keys", "kick", "lead-vocal", "mandolin", "marimba",
    "oboe", "organ", "percussion", "piano", "saxophone", "sitar", "snare", "strings", "synth",
    "tambourine", "timpani", "toms", "triangle", "trombone", "trumpet", "tuba", "ukulele", "viola",
    "violin", "vocal", "wind", "wind-chimes", "woodwind",
)

MEGA53_MODEL = dict(
    dim=256,
    depth=12,
    stereo=True,
    num_stems=53,
    time_transformer_depth=1,
    freq_transformer_depth=1,
    linear_transformer_depth=0,
    freqs_per_bands=(2,) * 24 + (4,) * 12 + (12,) * 8 + (24,) * 8 + (48,) * 8 + (128, 129),
    dim_head=64,
    heads=8,
    attn_dropout=0.0,
    ff_dropout=0.0,
    flash_attn=True,
    dim_freqs_in=1025,
    stft_n_fft=2048,
    stft_hop_length=512,
    stft_win_length=2048,
    stft_normalized=False,
    mask_estimator_depth=2,
    multi_stft_resolution_loss_weight=1.0,
    multi_stft_resolutions_window_sizes=(4096, 2048, 1024, 512, 256),
    multi_stft_hop_size=147,
    multi_stft_normalized=False,
    mlp_expansion_factor=2,
    use_torch_checkpoint=False,
    skip_connection=False,
)


def build_mdx23c():
    """Return an untrained MDX23C TFC-TDF v3 network for the DrumSep 6-stem checkpoint."""
    from .mdx23c_tfc_tdf_v3 import TFC_TDF_net

    return TFC_TDF_net(MDX23C_DRUMSEP)


def build_mega53():
    """Return an untrained BS-RoFormer network for the MVSep Mega 53-stem checkpoint."""
    from .bs_roformer import BSRoformer

    return BSRoformer(**MEGA53_MODEL)
