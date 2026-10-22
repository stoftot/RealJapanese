export function createPlayer(audio, button, status, source) {
    let disposed = false;
    const failed = () => {
        if (!disposed) status.textContent = "This clip could not be played. Try Play clip again or check the Audio Studio export.";
    };
    const play = async () => {
        if (disposed) return;
        audio.pause();
        audio.currentTime = 0;
        try {
            await audio.play();
            if (disposed) audio.pause();
            else status.textContent = "";
        } catch (error) {
            if (disposed || error.name === "AbortError") return;
            if (error.name === "NotAllowedError") status.textContent = "Tap Play clip to enable audio in this browser.";
            else failed();
        }
    };
    const dispose = () => {
        if (disposed) return;
        disposed = true;
        observer.disconnect();
        button.removeEventListener("click", play);
        audio.removeEventListener("error", failed);
        audio.pause();
        audio.removeAttribute("src");
        audio.load();
    };
    // A removed card must stop immediately, including navigation/disconnected circuits.
    const observer = new MutationObserver(() => { if (!audio.isConnected) dispose(); });
    observer.observe(document.body, { childList: true, subtree: true });
    audio.addEventListener("error", failed);
    button.addEventListener("click", play);
    audio.src = source;
    void play(); // One attempt per rendered question; ordinary rerenders do not replay it.
    return { dispose };
}
