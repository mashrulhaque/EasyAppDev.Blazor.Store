// Redux DevTools integration
let devToolsExtension = null;

export function initDevTools(storeName) {
    if (typeof window !== 'undefined' && window.__REDUX_DEVTOOLS_EXTENSION__) {
        try {
            devToolsExtension = window.__REDUX_DEVTOOLS_EXTENSION__.connect({
                name: storeName,
                features: {
                    pause: true,
                    lock: true,
                    persist: true,
                    export: true,
                    import: 'custom',
                    jump: true,
                    skip: true,
                    reorder: true,
                    dispatch: true,
                    test: true
                }
            });

            console.log(`Redux DevTools connected for store: ${storeName}`);
            return true;
        } catch (error) {
            console.warn('Redux DevTools initialization failed:', error);
            return false;
        }
    } else {
        console.warn('Redux DevTools Extension not found. Install from: https://github.com/reduxjs/redux-devtools');
        return false;
    }
}

export function sendToDevTools(actionType, stateJson) {
    if (devToolsExtension) {
        try {
            const state = JSON.parse(stateJson);
            devToolsExtension.send(
                {
                    type: actionType,
                    payload: {}
                },
                state
            );
        } catch (error) {
            console.error('Error sending to DevTools:', error);
        }
    }
}

export function disconnect() {
    if (devToolsExtension) {
        try {
            devToolsExtension.disconnect();
        } catch (error) {
            console.error('Error disconnecting DevTools:', error);
        }
        devToolsExtension = null;
    }
}
