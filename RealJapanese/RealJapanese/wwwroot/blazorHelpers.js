window.blazorHelpers = {
  _spaceShortcutHandlers: new Map(),
  _flashCardShortcutHandlers: new Map(),

  setValueAndFocus: function (el, val) {
    if (!el) return;
    try {
      el.value = val || "";
      el.focus();
    } catch (e) {
      console.error(e);
    }
  },

  registerSpaceShortcut: function (dotNetRef) {
    const id = `space-shortcut-${Date.now()}-${Math.random().toString(36).slice(2)}`;
    const handler = function (event) {
      if (event.code !== "Space" && event.key !== " ") return;

      const target = event.target;
      const tagName = target?.tagName?.toLowerCase?.() || "";
      const isInteractiveElement =
        tagName === "input" ||
        tagName === "textarea" ||
        tagName === "select" ||
        tagName === "button" ||
        tagName === "a" ||
        target?.isContentEditable;

      if (isInteractiveElement) return;

      event.preventDefault();
      dotNetRef.invokeMethodAsync("HandleGlobalSpaceAsync");
    };

    document.addEventListener("keydown", handler);
    this._spaceShortcutHandlers.set(id, handler);
    return id;
  },

  unregisterSpaceShortcut: function (id) {
    const handler = this._spaceShortcutHandlers.get(id);
    if (!handler) return;

    document.removeEventListener("keydown", handler);
    this._spaceShortcutHandlers.delete(id);
  },

  registerFlashCardShortcuts: function (dotNetRef) {
    const id = `flash-card-shortcuts-${Date.now()}-${Math.random().toString(36).slice(2)}`;
    const handler = function (event) {
      const isSpace = event.code === "Space" || event.key === " ";
      const isBackspace = event.key === "Backspace";

      if (!isSpace && !isBackspace) return;

      const target = event.target;
      const tagName = target?.tagName?.toLowerCase?.() || "";
      const isTextEntryElement =
        tagName === "input" ||
        tagName === "textarea" ||
        tagName === "select" ||
        target?.isContentEditable;

      if (isTextEntryElement) return;

      event.preventDefault();

      if (isBackspace) {
        dotNetRef.invokeMethodAsync("HandleGlobalBackspaceAsync");
      } else {
        dotNetRef.invokeMethodAsync("HandleGlobalSpaceAsync");
      }
    };

    document.addEventListener("keydown", handler);
    this._flashCardShortcutHandlers.set(id, handler);
    return id;
  },

  unregisterFlashCardShortcuts: function (id) {
    const handler = this._flashCardShortcutHandlers.get(id);
    if (!handler) return;

    document.removeEventListener("keydown", handler);
    this._flashCardShortcutHandlers.delete(id);
  }
};
