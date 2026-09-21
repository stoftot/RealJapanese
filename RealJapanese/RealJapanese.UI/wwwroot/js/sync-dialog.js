const dialogs = new WeakMap();

export function attach(element, receiver) {
    if (!element) return; // The page may have been removed while interop was in flight.
    const state = { generation: 0, dismissing: false };
    const dismiss = () => {
        if (!element.open || state.dismissing) return;
        state.dismissing = true;
        receiver.invokeMethodAsync('DismissFromBrowser', state.generation)
            .finally(() => { state.dismissing = false; });
    };
    state.cancel = event => { event.preventDefault(); dismiss(); };
    // Only a pointer gesture that starts and ends on the backdrop dismisses.
    const outside = event => {
        const bounds = element.getBoundingClientRect();
        return event.target === element && (event.clientX < bounds.left || event.clientX > bounds.right ||
            event.clientY < bounds.top || event.clientY > bounds.bottom);
    };
    state.down = event => { state.backdropStart = outside(event); };
    state.click = event => { if (state.backdropStart && outside(event)) dismiss(); state.backdropStart = false; };
    element.addEventListener('cancel', state.cancel);
    element.addEventListener('pointerdown', state.down);
    element.addEventListener('click', state.click);
    dialogs.set(element, state);
}

export function setOpen(element, open, generation) {
    const state = dialogs.get(element);
    state.generation = generation;
    if (open && !element.open) {
        element.showModal();
        element.querySelector('h2').focus({ preventScroll: true });
    } else if (!open && element.open) element.close();
}

export function detach(element) {
    const state = dialogs.get(element);
    if (!state) return;
    element.removeEventListener('cancel', state.cancel);
    element.removeEventListener('pointerdown', state.down);
    element.removeEventListener('click', state.click);
    if (element.open) element.close();
    dialogs.delete(element);
}
