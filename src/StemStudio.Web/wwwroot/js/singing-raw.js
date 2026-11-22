// Raw pitch readout of singing feedback (016 FR-041, ui-test-ids singing-raw, Expert): mirrors the pitch view host's
// engine-written data-singer-cents into every [data-singing-raw-for=<host id>] element as "220.0 Hz, A3 +3 cents", in
// the browser, without a server round trip per frame.
(() => {
  const names = ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B'];

  function describe(cents) {
    if (!Number.isFinite(cents) || cents <= 0) {
      return '';
    }
    const midi = Math.round(cents / 100);
    const offset = Math.round(cents - midi * 100);
    const hz = 440 * Math.pow(2, (cents / 100 - 69) / 12);
    const sign = offset >= 0 ? '+' : '';
    return `${hz.toFixed(1)} Hz, ${names[midi % 12]}${Math.floor(midi / 12) - 1} ${sign}${offset} cents`;
  }

  function update(host) {
    const raw = host.getAttribute('data-singer-cents');
    const text = raw === null || raw === '' ? '' : describe(Number(raw));
    document.querySelectorAll('[data-singing-raw-for]').forEach((el) => {
      if (el.getAttribute('data-singing-raw-for') === host.id) {
        el.textContent = text;
      }
    });
  }

  new MutationObserver((records) => {
    for (const record of records) {
      if (record.target instanceof Element && record.target.id) {
        update(record.target);
      }
    }
  }).observe(document.documentElement, { subtree: true, attributes: true, attributeFilter: ['data-singer-cents'] });
})();
