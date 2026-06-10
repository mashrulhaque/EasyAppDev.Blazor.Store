/**
 * Window event support for the EasyAppDev.Blazor.Store query system.
 * Notifies .NET when the window regains focus (RefetchOnWindowFocus) or the
 * network reconnects (RefetchOnReconnect), TanStack-Query style.
 *
 * Loaded as an ES module via JS interop:
 *   import('./_content/EasyAppDev.Blazor.Store/query-events.js')
 */

// Registrations keyed by an incrementing id so multiple QueryClients
// (e.g. multiple circuits/scopes in the same document) can coexist.
const registrations = new Map();
let nextId = 1;

// Guard window so a 'focus' event and a 'visibilitychange' event that fire
// together (typical when switching back to a tab) invoke .NET only once.
const FOCUS_DEBOUNCE_MS = 50;

/**
 * Registers window focus / reconnect listeners that call back into .NET.
 * @param {object} dotNetRef - DotNet object reference exposing
 *   OnWindowFocusAsync and OnReconnectAsync.
 * @returns {number} - Registration id to pass to dispose().
 */
export function init(dotNetRef) {
    const id = nextId++;
    let lastFocusAt = 0;

    const invokeFocus = () => {
        const now = Date.now();
        if (now - lastFocusAt < FOCUS_DEBOUNCE_MS) {
            return;
        }
        lastFocusAt = now;

        // A disposed circuit / dead dotNetRef must never surface as an
        // unhandled error - swallow both sync throws and async rejections.
        try {
            dotNetRef.invokeMethodAsync('OnWindowFocusAsync').catch(() => { });
        } catch {
            // Ignore - reference already disposed
        }
    };

    const onFocus = () => {
        if (typeof document === 'undefined' || document.visibilityState === 'visible') {
            invokeFocus();
        }
    };

    const onVisibilityChange = () => {
        if (document.visibilityState === 'visible') {
            invokeFocus();
        }
    };

    const onOnline = () => {
        try {
            dotNetRef.invokeMethodAsync('OnReconnectAsync').catch(() => { });
        } catch {
            // Ignore - reference already disposed
        }
    };

    window.addEventListener('focus', onFocus);
    document.addEventListener('visibilitychange', onVisibilityChange);
    window.addEventListener('online', onOnline);

    registrations.set(id, { onFocus, onVisibilityChange, onOnline });
    return id;
}

/**
 * Removes the listeners registered by init().
 * @param {number} id - The registration id returned by init().
 */
export function dispose(id) {
    const entry = registrations.get(id);
    if (!entry) {
        return;
    }

    window.removeEventListener('focus', entry.onFocus);
    document.removeEventListener('visibilitychange', entry.onVisibilityChange);
    window.removeEventListener('online', entry.onOnline);
    registrations.delete(id);
}
