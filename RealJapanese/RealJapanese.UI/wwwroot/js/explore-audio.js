const players = new Set();

export function createPlayer(audio, source) {
    const onPlay = () => {
        for (const other of players) {
            if (other !== audio) other.pause();
        }
    };
    players.add(audio);
    audio.addEventListener('play', onPlay);
    audio.src = source;

    return {
        dispose() {
            audio.removeEventListener('play', onPlay);
            audio.pause();
            audio.removeAttribute('src');
            audio.load();
            players.delete(audio);
        }
    };
}
