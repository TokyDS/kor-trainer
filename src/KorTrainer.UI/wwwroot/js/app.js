// Browser glue for KorTrainer: speech synthesis, keyboard shortcuts and theme.

const VOICE_WAIT_MS = 1500;
let koreanVoice = null;

const VOICE_KEY = 'kortrainer-voice';

function koreanOnly(voices) {
    return voices.filter(v => (v.lang || '').toLowerCase().replace('_', '-').startsWith('ko'));
}

function storedVoiceName() {
    try {
        return localStorage.getItem(VOICE_KEY);
    } catch {
        return null;
    }
}

function pickKoreanVoice(voices) {
    const korean = koreanOnly(voices);
    // The learner's choice wins (voices differ per device, so it is stored per browser);
    // otherwise prefer neural/network voices, which keep 가/카/까-style contrasts clearer.
    const chosen = storedVoiceName();
    return korean.find(v => v.name === chosen)
        ?? korean.find(v => /natural|neural|online|google/i.test(v.name))
        ?? korean[0]
        ?? null;
}

/** Names of all Korean voices installed in this browser. */
export async function koreanVoices() {
    return koreanOnly(await loadVoices()).map(v => ({ name: v.name, local: v.localService === true }));
}

/** Remembers the preferred Korean voice for this browser. */
export async function chooseVoice(name) {
    try {
        localStorage.setItem(VOICE_KEY, name);
    } catch {
        // Storage unavailable: the choice applies until the page is reloaded.
    }
    const voices = await loadVoices();
    koreanVoice = koreanOnly(voices).find(v => v.name === name) ?? pickKoreanVoice(voices);
    return koreanVoice ? koreanVoice.name : null;
}

function loadVoices() {
    if (!('speechSynthesis' in window)) {
        return Promise.resolve([]);
    }
    const voices = window.speechSynthesis.getVoices();
    if (voices.length > 0) {
        return Promise.resolve(voices);
    }
    // Chrome loads voices asynchronously and announces them with "voiceschanged".
    return new Promise(resolve => {
        const done = () => resolve(window.speechSynthesis.getVoices());
        window.speechSynthesis.addEventListener('voiceschanged', done, { once: true });
        setTimeout(done, VOICE_WAIT_MS);
    });
}

export async function speechInfo() {
    const supported = 'speechSynthesis' in window && 'SpeechSynthesisUtterance' in window;
    if (!supported) {
        return { supported: false, hasKoreanVoice: false, voiceName: null, platform: platformName() };
    }
    koreanVoice = pickKoreanVoice(await loadVoices());
    return {
        supported: true,
        hasKoreanVoice: koreanVoice !== null,
        voiceName: koreanVoice ? koreanVoice.name : null,
        platform: platformName(),
    };
}

// --- Native recordings ---------------------------------------------------------

const AUDIO_START_TIMEOUT_MS = 8000;
let player = null;

function stopRecording() {
    if (player) {
        player.pause();
        player.removeAttribute('src');
        player = null;
    }
}

/**
 * Plays a recording; resolves true once playback starts, false if it fails or takes too long
 * (the caller then falls back to speech synthesis).
 * @param {string} url
 * @returns {Promise<boolean>}
 */
export function playRecording(url) {
    if ('speechSynthesis' in window) {
        window.speechSynthesis.cancel();
    }
    stopRecording();
    const audio = new Audio(url);
    player = audio;
    return new Promise(resolve => {
        const timer = setTimeout(() => resolve(false), AUDIO_START_TIMEOUT_MS);
        audio.addEventListener('playing', () => { clearTimeout(timer); resolve(true); }, { once: true });
        audio.addEventListener('error', () => { clearTimeout(timer); resolve(false); }, { once: true });
        audio.play().catch(() => { clearTimeout(timer); resolve(false); });
    });
}

let recordingObjectUrl = null;

/**
 * Plays audio bytes streamed from .NET (the Android app has no HTTP server for recordings).
 * @param {{ arrayBuffer: () => Promise<ArrayBuffer> }} streamRef DotNetStreamReference
 * @param {string} contentType
 * @returns {Promise<boolean>}
 */
export async function playRecordingData(streamRef, contentType) {
    const bytes = await streamRef.arrayBuffer();
    if (recordingObjectUrl) {
        URL.revokeObjectURL(recordingObjectUrl);
    }
    recordingObjectUrl = URL.createObjectURL(new Blob([bytes], { type: contentType }));
    return playRecording(recordingObjectUrl);
}

/** Stops a recording, e.g. before the app speaks through the system voice. */
export function stopAudio() {
    stopRecording();
}

export function speak(text, rate) {
    if (!('speechSynthesis' in window) || !text) {
        return false;
    }
    stopRecording();
    window.speechSynthesis.cancel();
    const utterance = new SpeechSynthesisUtterance(text);
    utterance.lang = 'ko-KR';
    utterance.rate = rate || 0.9;
    if (koreanVoice) {
        utterance.voice = koreanVoice;
    }
    window.speechSynthesis.speak(utterance);
    return true;
}

function platformName() {
    const ua = navigator.userAgent || '';
    if (/android/i.test(ua)) return 'android';
    if (/iphone|ipad|ipod/i.test(ua)) return 'ios';
    if (/windows/i.test(ua)) return 'windows';
    if (/mac os/i.test(ua)) return 'macos';
    if (/linux/i.test(ua)) return 'linux';
    return 'other';
}

// --- Keyboard ---------------------------------------------------------------

const handlers = new Map();
let nextHandlerId = 1;

function isTyping(target) {
    if (!target) return false;
    const tag = (target.tagName || '').toLowerCase();
    return tag === 'input' || tag === 'textarea' || tag === 'select' || target.isContentEditable;
}

function normalizeKey(event) {
    if (event.code === 'Space' || event.key === ' ') return 'Space';
    if (event.key === 'Enter') return 'Enter';
    if (event.key === 'Escape') return 'Escape';
    if (/^Digit[1-9]$/.test(event.code)) return event.code.substring(5);
    if (/^Numpad[1-9]$/.test(event.code)) return event.code.substring(6);
    if (/^Key[A-Z]$/.test(event.code)) return event.code.substring(3).toLowerCase();
    return null;
}

export function registerKeys(dotNetRef) {
    const handler = event => {
        // Holding a key must not answer, advance or reload several times.
        if (event.repeat || event.ctrlKey || event.altKey || event.metaKey || isTyping(event.target)) {
            return;
        }
        const key = normalizeKey(event);
        if (key === null) {
            return;
        }
        // Space would scroll and Enter would re-click the focused button.
        if (key === 'Space' || key === 'Enter') {
            event.preventDefault();
        }
        dotNetRef.invokeMethodAsync('OnKey', key);
    };
    const id = nextHandlerId++;
    handlers.set(id, handler);
    document.addEventListener('keydown', handler);
    return id;
}

export function unregisterKeys(id) {
    const handler = handlers.get(id);
    if (handler) {
        document.removeEventListener('keydown', handler);
        handlers.delete(id);
    }
}

// --- Theme ------------------------------------------------------------------

export function applyTheme(theme) {
    const root = document.documentElement;
    if (theme === 'Light' || theme === 'Dark') {
        root.dataset.theme = theme.toLowerCase();
    } else {
        delete root.dataset.theme;
    }
    try {
        localStorage.setItem('kortrainer-theme', theme);
    } catch {
        // Storage can be unavailable (private mode); the theme still applies for this visit.
    }
}

export function blurActive() {
    if (document.activeElement instanceof HTMLElement) {
        document.activeElement.blur();
    }
}
