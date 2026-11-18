// Copies the engine's data-peak-db into aria-valuenow / aria-valuetext of the input meter at a limited rate
// (015 FR-021: the level is exposed to assistive technology at most twice a second).
export function watchMeter(id, intervalMs) {
  const timer = setInterval(() => {
    const meter = document.getElementById(id);
    const peak = Number.parseFloat(meter?.dataset.peakDb ?? '');
    if (!meter || Number.isNaN(peak)) {
      return;
    }
    const value = Math.max(-60, Math.min(0, Math.round(peak)));
    meter.setAttribute('aria-valuenow', String(value));
    meter.setAttribute('aria-valuetext', `${value} dB`);
  }, intervalMs);
  return { dispose: () => clearInterval(timer) };
}
