// Focus trap sentinels of the modal dialogs (013 FR-032, WCAG 2.4.3; Components/Care/FocusTrap.razor). The sentinels'
// Blazor @onfocus handlers move the focus over the server connection, so a fast second Tab could leave the dialog before
// they ran (013 CTO a11y finding). This moves it at once, in the browser, inside the same focus event: the end sentinel
// sends the focus to the dialog's title (aria-labelledby), the start sentinel to the anchor after the last control. The
// Blazor handlers stay as the fallback and land on the same element.
(() => {
  const titleOf = (dialog) => {
    const id = dialog.getAttribute('aria-labelledby');
    const title = id ? document.getElementById(id) : null;
    return title ?? dialog.querySelector('[tabindex="-1"]:not([data-focus-sentinel])');
  };
  document.addEventListener('focusin', (event) => {
    const sentinel = event.target instanceof Element ? event.target.closest('[data-focus-sentinel]') : null;
    const dialog = sentinel?.closest('[aria-modal="true"]');
    if (!sentinel || !dialog) {
      return;
    }
    const kind = sentinel.getAttribute('data-focus-sentinel');
    const target = kind === 'end' ? titleOf(dialog) : kind === 'start' ? dialog.querySelector('[data-focus-sentinel="end-anchor"]') : null;
    target?.focus();
  });
})();
