// Mixer keyboard shortcuts (frontend, T112). Space toggles play/pause unless a control has focus,
// so typing in inputs and activating buttons with space keep their native behaviour.
const ignored = 'input, textarea, select, button, a, [contenteditable="true"], [role="dialog"] *';

/**
 * Listens for the space key and calls `OnSpace` on the given .NET object.
 * @param {{ invokeMethodAsync(name: string): Promise<unknown> }} dotnetRef
 * @returns {{ dispose(): void }} Removes the listener.
 */
export function registerSpace(dotnetRef) {
  const onKeyDown = (event) => {
    if (event.code !== 'Space' || event.repeat || event.target?.closest?.(ignored)) {
      return;
    }
    event.preventDefault();
    dotnetRef.invokeMethodAsync('OnSpace');
  };
  document.addEventListener('keydown', onKeyDown);
  return { dispose: () => document.removeEventListener('keydown', onKeyDown) };
}
