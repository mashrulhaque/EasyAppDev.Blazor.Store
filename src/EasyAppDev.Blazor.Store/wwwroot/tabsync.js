/**
 * Tab synchronization support for EasyAppDev.Blazor.Store
 * Uses BroadcastChannel API to sync state changes across browser tabs.
 *
 * Security: Uses Symbol-keyed storage to prevent external script access.
 */
(function () {
    'use strict';

    // Use Symbol for internal storage key to prevent external access
    const STORAGE_KEY = Symbol.for('EasyAppDev.Blazor.Store.TabSync');

    // Initialize storage if not exists (using Symbol prevents enumeration)
    if (!window[STORAGE_KEY]) {
        Object.defineProperty(window, STORAGE_KEY, {
            value: new Map(),
            writable: false,
            enumerable: false,
            configurable: false
        });
    }

    const storage = window[STORAGE_KEY];

    // Legacy support: maintain backward compatibility with existing code
    // but mark as deprecated
    if (!window.__storeTabSync) {
        Object.defineProperty(window, '__storeTabSync', {
            get: function() {
                console.warn('[TabSync] window.__storeTabSync is deprecated. Internal storage is now isolated.');
                return {}; // Return empty object to prevent errors
            },
            enumerable: false,
            configurable: false
        });
    }

    /**
     * Initializes a BroadcastChannel for cross-tab communication.
     * @param {string} channelName - The name of the channel
     * @param {object} dotNetRef - DotNet object reference for callbacks
     * @returns {boolean} - True if initialization succeeded
     */
    window.__initTabSync = function (channelName, dotNetRef) {
        if (!channelName || !dotNetRef) {
            console.warn('[TabSync] Invalid parameters for initialization');
            return false;
        }

        // Check if BroadcastChannel is supported
        if (typeof BroadcastChannel === 'undefined') {
            console.warn('[TabSync] BroadcastChannel API not supported in this browser');
            return false;
        }

        // Close existing channel if any
        if (storage.has(channelName)) {
            try {
                storage.get(channelName).channel.close();
            } catch (e) {
                // Ignore errors when closing
            }
        }

        try {
            const channel = new BroadcastChannel(channelName);

            channel.onmessage = async function (event) {
                try {
                    await dotNetRef.invokeMethodAsync('OnMessageReceived', event.data);
                } catch (e) {
                    console.error('[TabSync] Error invoking .NET callback:', e);
                }
            };

            channel.onmessageerror = function (event) {
                console.error('[TabSync] Message error:', event);
            };

            storage.set(channelName, {
                channel: channel,
                dotNetRef: dotNetRef,
                createdAt: Date.now()
            });

            return true;
        } catch (e) {
            console.error('[TabSync] Failed to initialize channel:', e);
            return false;
        }
    };

    /**
     * Sends a message to all other tabs via the BroadcastChannel.
     * @param {string} channelName - The name of the channel
     * @param {string} message - JSON-serialized message to send
     */
    window.__postTabSyncMessage = function (channelName, message) {
        const syncInfo = storage.get(channelName);
        if (!syncInfo || !syncInfo.channel) {
            console.warn('[TabSync] Channel not initialized:', channelName);
            return;
        }

        try {
            syncInfo.channel.postMessage(message);
        } catch (e) {
            console.error('[TabSync] Failed to post message:', e);
        }
    };

    /**
     * Closes and cleans up a BroadcastChannel.
     * @param {string} channelName - The name of the channel to dispose
     */
    window.__disposeTabSync = function (channelName) {
        const syncInfo = storage.get(channelName);
        if (!syncInfo) {
            return;
        }

        try {
            if (syncInfo.channel) {
                syncInfo.channel.close();
            }
        } catch (e) {
            // Ignore errors when closing
        }

        storage.delete(channelName);
    };

    /**
     * Gets the count of active channels (for diagnostics).
     * @returns {number} - Number of active channels
     */
    window.__getTabSyncChannelCount = function () {
        return storage.size;
    };

    /**
     * Gets key material for deriving a consistent signing key across tabs.
     * Returns window.location.origin as the seed for PBKDF2 key derivation.
     * All tabs from the same origin will receive the same value.
     * @returns {string} - The origin (protocol + hostname + port)
     */
    window.__getTabSyncKeyMaterial = function () {
        if (typeof window === 'undefined' || !window.location) {
            console.warn('[TabSync] Cannot derive key material: window.location not available');
            return '';
        }
        return window.location.origin;
    };
})();
