/**
 * Tab synchronization support for EasyAppDev.Blazor.Store
 * Uses BroadcastChannel API to sync state changes across browser tabs.
 */
(function () {
    'use strict';

    // Store for active channels
    window.__storeTabSync = window.__storeTabSync || {};

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
        if (window.__storeTabSync[channelName]) {
            try {
                window.__storeTabSync[channelName].channel.close();
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

            window.__storeTabSync[channelName] = {
                channel: channel,
                dotNetRef: dotNetRef
            };

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
        const syncInfo = window.__storeTabSync[channelName];
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
        const syncInfo = window.__storeTabSync[channelName];
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

        delete window.__storeTabSync[channelName];
    };
})();
