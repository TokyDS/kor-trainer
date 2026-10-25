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
export function playRecording(url, rate = 1) {
    if ('speechSynthesis' in window) {
        window.speechSynthesis.cancel();
    }
    stopRecording();
    const audio = new Audio(url);
    // Slower playback keeps the pitch (preservesPitch is on by default).
    audio.playbackRate = rate;
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
export async function playRecordingData(streamRef, contentType, rate = 1) {
    const bytes = await streamRef.arrayBuffer();
    if (recordingObjectUrl) {
        URL.revokeObjectURL(recordingObjectUrl);
    }
    recordingObjectUrl = URL.createObjectURL(new Blob([bytes], { type: contentType }));
    return playRecording(recordingObjectUrl, rate);
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

// --- Keyboard trainer (두벌식) -----------------------------------------------------

const typingHandlers = new WeakMap();

/**
 * Sends physical letter keys and Backspace of a hardware keyboard to .NET, which composes Hangul itself,
 * so no Korean layout has to be installed. Phone keyboards send no key codes (keyCode 229) and type into
 * the input normally; .NET then reads the value from the input event.
 * @param {HTMLInputElement} input
 */
export function attachTyping(input, dotnetRef) {
    if (!input || typingHandlers.has(input)) return;
    const handler = event => {
        if (event.ctrlKey || event.metaKey || event.altKey || event.isComposing) return;
        if (/^Key[A-Z]$/.test(event.code) || event.code === 'Backspace') {
            event.preventDefault();
            dotnetRef.invokeMethodAsync('OnTypingKey', event.code, event.shiftKey);
        }
    };
    input.addEventListener('keydown', handler);
    typingHandlers.set(input, handler);
    input.focus();
}

export function detachTyping(input) {
    const handler = input && typingHandlers.get(input);
    if (handler) {
        input.removeEventListener('keydown', handler);
        typingHandlers.delete(input);
    }
}

export function focusElement(element) {
    element?.focus();
}

// --- Speech recognition (shadowing) ---------------------------------------------

const RECOGNITION_TIMEOUT_MS = 12000;

function recognitionClass() {
    return window.SpeechRecognition || window.webkitSpeechRecognition || null;
}

export function speechRecognitionAvailable() {
    return recognitionClass() !== null;
}

const recognitionErrors = {
    'not-allowed': 'Нет доступа к микрофону — разрешите его в настройках браузера.',
    'service-not-allowed': 'Распознавание речи запрещено в этом браузере.',
    'no-speech': 'Не услышал речь — попробуйте ещё раз, ближе к микрофону.',
    'audio-capture': 'Микрофон не найден.',
    'network': 'Для распознавания нужен интернет.',
    'language-not-supported': 'Корейский язык распознавания не поддерживается.',
};

/**
 * Listens for one Korean utterance.
 * @returns {Promise<{text: string|null, error: string|null}>}
 */
export function recognizeKorean() {
    const Recognition = recognitionClass();
    if (!Recognition) {
        return Promise.resolve({ text: null, error: 'Браузер не умеет распознавать речь — откройте тренажёр в Chrome.' });
    }
    stopRecording();
    if ('speechSynthesis' in window) {
        window.speechSynthesis.cancel();
    }
    return new Promise(resolve => {
        const recognition = new Recognition();
        recognition.lang = 'ko-KR';
        recognition.interimResults = false;
        recognition.maxAlternatives = 1;
        let settled = false;
        const finish = value => {
            if (!settled) {
                settled = true;
                clearTimeout(timer);
                resolve(value);
            }
        };
        const timer = setTimeout(() => { recognition.abort(); finish({ text: null, error: recognitionErrors['no-speech'] }); }, RECOGNITION_TIMEOUT_MS);
        recognition.onresult = event => finish({ text: event.results[0][0].transcript, error: null });
        recognition.onerror = event => finish({ text: null, error: recognitionErrors[event.error] ?? `Ошибка распознавания: ${event.error}` });
        recognition.onend = () => finish({ text: null, error: recognitionErrors['no-speech'] });
        recognition.start();
    });
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

/** Brings an element into view, centred; instantly when the learner prefers reduced motion. */
export function scrollToElement(element) {
    const smooth = !window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    element?.scrollIntoView({ block: 'center', behavior: smooth ? 'smooth' : 'auto' });
}

export function blurActive() {
    if (document.activeElement instanceof HTMLElement) {
        document.activeElement.blur();
    }
}

// --- Swipes on word cards ---------------------------------------------------------

const SWIPE_DISTANCE = 80;
const swipes = new WeakMap();

/** The card follows the finger; on release reports 'left', 'right', 'up' or 'tap' to OnSwipe. */
export function attachSwipe(element, dotNetRef) {
    if (!element || swipes.has(element)) {
        return;
    }

    let start = null;
    const reset = () => {
        element.style.transform = '';
        element.classList.remove('swiping');
    };
    const down = e => {
        if (e.target.closest('button, a, input')) {
            return; // buttons inside the card keep their clicks
        }
        start = { x: e.clientX, y: e.clientY };
        element.setPointerCapture?.(e.pointerId);
        element.classList.add('swiping');
    };
    const move = e => {
        if (!start) {
            return;
        }
        const dx = e.clientX - start.x;
        const dy = Math.min(0, e.clientY - start.y);
        element.style.transform = `translate(${dx}px, ${dy}px) rotate(${dx / 20}deg)`;
    };
    const up = e => {
        if (!start) {
            return;
        }
        const dx = e.clientX - start.x;
        const dy = e.clientY - start.y;
        start = null;
        reset();
        const direction = dx > SWIPE_DISTANCE ? 'right'
            : dx < -SWIPE_DISTANCE ? 'left'
            : dy < -SWIPE_DISTANCE ? 'up'
            : Math.abs(dx) < 10 && Math.abs(dy) < 10 ? 'tap'
            : null;
        if (direction) {
            dotNetRef.invokeMethodAsync('OnSwipe', direction);
        }
    };
    const cancel = () => {
        start = null;
        reset();
    };
    element.addEventListener('pointerdown', down);
    element.addEventListener('pointermove', move);
    element.addEventListener('pointerup', up);
    element.addEventListener('pointercancel', cancel);
    swipes.set(element, () => {
        element.removeEventListener('pointerdown', down);
        element.removeEventListener('pointermove', move);
        element.removeEventListener('pointerup', up);
        element.removeEventListener('pointercancel', cancel);
    });
}

export function detachSwipe(element) {
    swipes.get(element)?.();
    swipes.delete(element);
}

// --- Scroll memory ----------------------------------------------------------------

const scrollPositions = new Map();
let restorePending = false;
let scrollWatched = false;

/** Remembers how far each page was scrolled; a back step (popstate) asks for it to come back. */
export function rememberScroll() {
    if (scrollWatched) {
        return;
    }
    scrollWatched = true;
    window.addEventListener('scroll', () => scrollPositions.set(location.pathname + location.search, window.scrollY), { passive: true });
    window.addEventListener('popstate', () => { restorePending = true; });
}

/** After a back step, scrolls to where the page was, once its content is tall enough (data may still be arriving). */
export function restoreScroll() {
    if (!restorePending) {
        return;
    }
    restorePending = false;
    const y = scrollPositions.get(location.pathname + location.search) ?? 0;
    let tries = 0;
    const attempt = () => {
        if (document.documentElement.scrollHeight - window.innerHeight >= y || tries >= 20) {
            window.scrollTo(0, y);
            return;
        }
        tries++;
        setTimeout(attempt, 50);
    };
    requestAnimationFrame(attempt);
}

// --- Network ----------------------------------------------------------------------

/** Reports the browser's online state now and on every change. */
export function watchOnline(dotNetRef) {
    const report = () => dotNetRef.invokeMethodAsync('SetOnline', navigator.onLine);
    window.addEventListener('online', report);
    window.addEventListener('offline', report);
    report();
}

// --- Device preferences and answer feedback ---------------------------------------

const PREFERENCES_KEY = 'kortrainer-device';

export function loadPreferences() {
    try {
        return localStorage.getItem(PREFERENCES_KEY);
    } catch {
        return null; // storage blocked: defaults apply
    }
}

export function savePreferences(json) {
    try {
        localStorage.setItem(PREFERENCES_KEY, json);
    } catch {
        // Storage can be unavailable (private mode); the choice still applies for this visit.
    }
}

/** Korean text size, as a factor of the default size. */
export function applyKoreanScale(scale) {
    document.documentElement.style.setProperty('--ko-scale', String(scale));
}

/** A short buzz where the browser supports vibration. */
export function buzz() {
    if (navigator.vibrate) {
        navigator.vibrate(60);
    }
}

let chimeContext;

/** A soft two-note chime for a right answer. */
export function chime() {
    try {
        chimeContext ??= new AudioContext();
        const now = chimeContext.currentTime;
        [[660, 0], [990, 0.09]].forEach(([frequency, offset]) => {
            const tone = chimeContext.createOscillator();
            const gain = chimeContext.createGain();
            tone.type = 'sine';
            tone.frequency.value = frequency;
            gain.gain.setValueAtTime(0.0001, now + offset);
            gain.gain.exponentialRampToValueAtTime(0.12, now + offset + 0.02);
            gain.gain.exponentialRampToValueAtTime(0.0001, now + offset + 0.22);
            tone.connect(gain).connect(chimeContext.destination);
            tone.start(now + offset);
            tone.stop(now + offset + 0.25);
        });
    } catch {
        // No Web Audio: the answer is still shown.
    }
}
