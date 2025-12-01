// Enhanced DevTools integration with time-travel support

let devToolsConnection = null;
let dotNetRef = null;
let storeOptions = null;

export function initEnhancedDevTools(optionsJson, dotNetReference) {
    try {
        storeOptions = JSON.parse(optionsJson);
        dotNetRef = dotNetReference;

        if (typeof window === 'undefined' || !window.__REDUX_DEVTOOLS_EXTENSION__) {
            console.debug('Redux DevTools extension not available');
            return;
        }

        devToolsConnection = window.__REDUX_DEVTOOLS_EXTENSION__.connect({
            name: storeOptions.name,
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

        // Subscribe to DevTools events
        devToolsConnection.subscribe(async (message) => {
            if (!message) return;

            try {
                switch (message.type) {
                    case 'DISPATCH':
                        await handleDispatch(message.payload, message.state);
                        break;
                    case 'ACTION':
                        // Direct action from DevTools
                        if (dotNetRef && storeOptions.features?.dispatch) {
                            await dotNetRef.invokeMethodAsync('ReplayAction', message.payload);
                        }
                        break;
                }
            } catch (error) {
                console.error('Error handling DevTools message:', error);
            }
        });

        // Send initial state
        devToolsConnection.init({});

        console.debug(`Enhanced DevTools connected for store: ${storeOptions.name}`);
    } catch (error) {
        console.error('Failed to initialize enhanced DevTools:', error);
    }
}

async function handleDispatch(payload, state) {
    if (!payload) return;

    switch (payload.type) {
        case 'JUMP_TO_ACTION':
        case 'JUMP_TO_STATE':
            if (dotNetRef && storeOptions.features?.jump) {
                await dotNetRef.invokeMethodAsync('JumpToStateAsync', payload.actionId);
            }
            break;

        case 'TOGGLE_ACTION':
            // Skip/unskip an action
            if (dotNetRef && storeOptions.features?.skip) {
                console.debug('Toggle action:', payload.id);
            }
            break;

        case 'IMPORT_STATE':
            if (dotNetRef && storeOptions.features?.import && state) {
                const computedStates = JSON.parse(state).computedStates;
                if (computedStates && computedStates.length > 0) {
                    const lastState = computedStates[computedStates.length - 1].state;
                    await dotNetRef.invokeMethodAsync('ImportStateAsync', JSON.stringify(lastState));
                }
            }
            break;

        case 'COMMIT':
            // Commit current state as new base
            console.debug('Commit requested');
            break;

        case 'ROLLBACK':
            // Rollback to last committed state
            if (dotNetRef) {
                await dotNetRef.invokeMethodAsync('JumpToStateAsync', 0);
            }
            break;

        case 'RESET':
            // Reset to initial state
            if (dotNetRef) {
                await dotNetRef.invokeMethodAsync('JumpToStateAsync', 0);
            }
            break;
    }
}

export function sendEnhancedAction(actionJson, stateJson, performanceJson) {
    if (!devToolsConnection) return;

    try {
        const action = JSON.parse(actionJson);
        const state = JSON.parse(stateJson);

        // Add performance info to action if available
        if (performanceJson) {
            const perf = JSON.parse(performanceJson);
            action._performance = perf;
        }

        devToolsConnection.send(action, state);
    } catch (error) {
        console.error('Failed to send action to DevTools:', error);
    }
}

export function pauseRecording() {
    if (devToolsConnection) {
        devToolsConnection.pause();
    }
}

export function resumeRecording() {
    if (devToolsConnection) {
        devToolsConnection.resume();
    }
}

export function exportState() {
    if (devToolsConnection) {
        return devToolsConnection.export();
    }
    return null;
}
