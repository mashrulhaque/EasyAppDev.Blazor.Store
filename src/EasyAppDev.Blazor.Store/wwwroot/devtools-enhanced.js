// Enhanced DevTools integration with time-travel support
// Per-store entries are kept in a Map keyed by store name so multiple stores in
// the same app each get their own DevTools connection.
const stores = new Map(); // storeName -> { connection, dotNetRef, options }

export function initEnhancedDevTools(optionsJson, dotNetReference) {
    try {
        const storeOptions = JSON.parse(optionsJson);
        const storeName = storeOptions.name;

        if (typeof window === 'undefined' || !window.__REDUX_DEVTOOLS_EXTENSION__) {
            console.debug('Redux DevTools extension not available');
            return;
        }

        if (stores.has(storeName)) {
            // Already connected for this store
            return;
        }

        const connection = window.__REDUX_DEVTOOLS_EXTENSION__.connect({
            name: storeName,
            maxAge: storeOptions.maxAge || 100,
            features: storeOptions.features || {
                jump: true,
                skip: true,
                dispatch: true,
                persist: true,
                export: true,
                import: true
            }
        });

        const entry = {
            connection: connection,
            dotNetRef: dotNetReference,
            options: storeOptions
        };
        stores.set(storeName, entry);

        // Subscribe to DevTools events
        connection.subscribe(async (message) => {
            if (!message) return;

            try {
                switch (message.type) {
                    case 'DISPATCH':
                        await handleDispatch(entry, message.payload, message.state);
                        break;
                    case 'ACTION':
                        // Direct action from DevTools
                        if (entry.dotNetRef && entry.options.features?.dispatch) {
                            await entry.dotNetRef.invokeMethodAsync('ReplayAction', message.payload);
                        }
                        break;
                }
            } catch (error) {
                console.error('Error handling DevTools message:', error);
            }
        });

        // Send initial state
        connection.init({});

        console.debug(`Enhanced DevTools connected for store: ${storeName}`);
    } catch (error) {
        console.error('Failed to initialize enhanced DevTools:', error);
    }
}

async function handleDispatch(entry, payload, state) {
    if (!payload) return;

    switch (payload.type) {
        case 'JUMP_TO_ACTION':
        case 'JUMP_TO_STATE':
            if (entry.dotNetRef && entry.options.features?.jump) {
                await entry.dotNetRef.invokeMethodAsync('JumpToStateAsync', payload.actionId);
            }
            break;

        case 'TOGGLE_ACTION':
            // Skip/unskip an action
            if (entry.dotNetRef && entry.options.features?.skip) {
                console.debug('Toggle action:', payload.id);
            }
            break;

        case 'IMPORT_STATE':
            if (entry.dotNetRef && entry.options.features?.import && state) {
                const computedStates = JSON.parse(state).computedStates;
                if (computedStates && computedStates.length > 0) {
                    const lastState = computedStates[computedStates.length - 1].state;
                    await entry.dotNetRef.invokeMethodAsync('ImportStateAsync', JSON.stringify(lastState));
                }
            }
            break;

        case 'COMMIT':
            // Commit current state as new base
            console.debug('Commit requested');
            break;

        case 'ROLLBACK':
            // Rollback to last committed state
            if (entry.dotNetRef) {
                await entry.dotNetRef.invokeMethodAsync('JumpToStateAsync', 0);
            }
            break;

        case 'RESET':
            // Reset to initial state
            if (entry.dotNetRef) {
                await entry.dotNetRef.invokeMethodAsync('JumpToStateAsync', 0);
            }
            break;
    }
}

export function sendEnhancedAction(storeName, actionJson, stateJson, performanceJson) {
    const entry = stores.get(storeName);
    if (!entry) return;

    try {
        const action = JSON.parse(actionJson);
        const state = JSON.parse(stateJson);

        // Add performance info to action if available
        if (performanceJson) {
            const perf = JSON.parse(performanceJson);
            action._performance = perf;
        }

        entry.connection.send(action, state);
    } catch (error) {
        console.error('Failed to send action to DevTools:', error);
    }
}

export function pauseRecording(storeName) {
    const entry = stores.get(storeName);
    if (entry) {
        entry.connection.pause();
    }
}

export function resumeRecording(storeName) {
    const entry = stores.get(storeName);
    if (entry) {
        entry.connection.resume();
    }
}

export function exportState(storeName) {
    const entry = stores.get(storeName);
    if (entry) {
        return entry.connection.export();
    }
    return null;
}

export function disconnect(storeName) {
    const entry = stores.get(storeName);
    if (entry) {
        try {
            entry.connection.unsubscribe?.();
            entry.connection.disconnect?.();
        } catch (error) {
            console.error('Error disconnecting enhanced DevTools:', error);
        }
        stores.delete(storeName);
    }
}
