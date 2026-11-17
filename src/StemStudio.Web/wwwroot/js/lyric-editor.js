// Lyrics editor helpers of the lyric assistant (feature 023, frontend). Selection of the text area and the browser
// defaults of the assistant keys; the actions themselves run in .NET (@onkeydown), so nothing here changes the text.

/** Keys of ui-test-ids 1.23.0 "Keyboard" whose browser default must not run (AltGr/Ctrl+Alt+C typing, Alt menus). */
function isAssistKey(event) {
  if (event.key === 'F8') {
    return true;
  }
  if (event.altKey && !event.ctrlKey && (event.key === 'Enter' || event.key === 'Backspace' || event.key.toLowerCase() === 'i')) {
    return true;
  }
  return event.ctrlKey && event.altKey && event.key.toLowerCase() === 'c';
}

/**
 * Stops the browser defaults of the assistant keys inside `root`.
 * @param {HTMLElement} root The editor element.
 * @returns {{ dispose(): void }} Removes the listener.
 */
export function bindKeys(root) {
  const onKeyDown = (event) => {
    if (isAssistKey(event)) {
      event.preventDefault();
    }
  };
  root?.addEventListener('keydown', onKeyDown);
  return { dispose: () => root?.removeEventListener('keydown', onKeyDown) };
}

/**
 * The selection of a text area.
 * @param {HTMLTextAreaElement} area The text area.
 * @returns {number[]} Start and end offsets.
 */
export function getSelection(area) {
  return area ? [area.selectionStart, area.selectionEnd] : [0, 0];
}

/**
 * Selects a range of a text area, focuses it and scrolls the range into view.
 * @param {HTMLTextAreaElement} area The text area.
 * @param {number} start Start offset.
 * @param {number} end End offset.
 */
export function select(area, start, end) {
  if (!area) {
    return;
  }
  area.focus({ preventScroll: false });
  area.setSelectionRange(start, end);
}

/**
 * Focuses an element by id without scrolling the page under the fixed bars.
 * @param {string} id The element id.
 */
export function focusById(id) {
  document.getElementById(id)?.focus({ preventScroll: false });
}
