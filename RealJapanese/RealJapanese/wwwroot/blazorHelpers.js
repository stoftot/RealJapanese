window.blazorHelpers = {
  setValueAndFocus: function (el, val) {
    if (!el) return;
    try {
      el.value = val || "";
      el.focus();
    } catch (e) {
      console.error(e);
    }
  }
};
