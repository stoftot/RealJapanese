window.genkiStudio = { scrollAndFocus: (section, heading) => { section?.scrollIntoView({ behavior: 'smooth', block: 'center' }); heading?.focus({ preventScroll: true }); } };
