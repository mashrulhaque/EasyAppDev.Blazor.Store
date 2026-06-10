// Redux DevTools integration
// Connections are kept in a Map keyed by store name so multiple stores in the
// same app each get their own DevTools connection (module-level singletons used
// to clobber each other in multi-store apps).
const connections = new Map();

export function initDevTools(storeName) {
    if (typeof window !== 'undefined' && window.__REDUX_DEVTOOLS_EXTENSION__) {
        try {
            if (connections.has(storeName)) {
                // Already connected for this store
                return true;
            }

            const connection = window.__REDUX_DEVTOOLS_EXTENSION__.connect({
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

            connections.set(storeName, connection);

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

export function sendToDevTools(storeName, actionType, stateJson) {
    const connection = connections.get(storeName);
    if (connection) {
        try {
            const state = JSON.parse(stateJson);
            connection.send(
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

export function disconnect(storeName) {
    const connection = connections.get(storeName);
    if (connection) {
        try {
            connection.disconnect();
        } catch (error) {
            console.error('Error disconnecting DevTools:', error);
        }
        connections.delete(storeName);
    }
}
