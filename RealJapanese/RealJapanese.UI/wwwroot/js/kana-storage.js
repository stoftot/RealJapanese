const key = "realjapanese.kana.v1";

export function load() {
    try {
        return { json: localStorage.getItem(key), available: true };
    } catch {
        return { json: null, available: false };
    }
}

export function save(json) {
    try {
        localStorage.setItem(key, json);
        return true;
    } catch {
        return false;
    }
}
