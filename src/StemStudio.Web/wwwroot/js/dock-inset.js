// Mixer dock inset (012 integration, WCAG 2.4.11 Focus Not Obscured). The karaoke banner, the count-in slot and the
// transport stick to the bottom of the viewport together (above the bottom navigation bar on small screens); their
// height depends on the banner's text size, the lyrics and the width. This keeps `--ss-dock-inset` on <html> equal to
// the distance from the viewport bottom to the dock's top (sticky offset + height), so `scroll-padding-bottom`
// (css/components/mixer.css, karaoke.css) scrolls every focused control above the dock. Visual layout only; without
// this script the CSS falls back to the fixed transport height.
(() => {
  const root = document.documentElement;
  let observed = null;
  const measure = (dock) => {
    const offset = parseFloat(getComputedStyle(dock).bottom) || 0;
    root.style.setProperty('--ss-dock-inset', `${Math.ceil(offset + dock.getBoundingClientRect().height)}px`);
  };
  const sizer = new ResizeObserver((entries) => {
    measure(entries[entries.length - 1].target);
  });
  const sync = () => {
    const dock = document.querySelector('.ss-mixer-dock, .ss-transport-dock');
    if (dock === observed) {
      return;
    }
    sizer.disconnect();
    observed = dock;
    if (dock === null) {
      root.style.removeProperty('--ss-dock-inset');
    } else {
      sizer.observe(dock);
    }
  };
  new MutationObserver(sync).observe(document.body, { childList: true, subtree: true });
  // The sticky offset changes with the small-screen breakpoint without resizing the dock.
  window.addEventListener('resize', () => {
    if (observed !== null) {
      measure(observed);
    }
  });
  sync();
})();
