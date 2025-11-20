(() => {
    const maximumRetryCount = 30;
    const retryIntervalMilliseconds = 1000;
    const reconnectModal = document.getElementById('reconnect-modal');
    const reconnectString = document.currentScript.dataset.reconnectString;

    async function attemptReload() {
        await fetch(''); // Check the server really is back
        location.reload();
    }

    const startReconnectionProcess = () => {
        reconnectModal.style.display = 'flex';

        let isCanceled = false;

        (async () => {
            for (let i = 1; i <= maximumRetryCount; i++) {
                const messageDiv = document.createElement('div');
                messageDiv.textContent = reconnectString;

                const countDiv = document.createElement('div');
                countDiv.textContent = `${i} of ${maximumRetryCount}`;

                reconnectModal.replaceChildren(messageDiv, countDiv);

                await new Promise(resolve => setTimeout(resolve, retryIntervalMilliseconds));

                if (isCanceled) {
                    return;
                }

                try {
                    const result = await Blazor.reconnect();
                    if (!result) {
                        // The server was reached, but the connection was rejected; reload the page.
                        attemptReload();
                        setInterval(attemptReload, 1000);
                        return;
                    }

                    // Successfully reconnected to the server.
                    return;
                } catch {
                    // Didn't reach the server; try again.
                }
            }

            // Retried too many times; reload the page.
            location.reload();
        })();

        return {
            cancel: () => {
                isCanceled = true;
                reconnectModal.style.display = 'none';
            },
        };
    };

    let currentReconnectionProcess = null;

    Blazor.start({
        circuit: {
            reconnectionHandler: {
                onConnectionDown: () => currentReconnectionProcess ??= startReconnectionProcess(),
                onConnectionUp: () => {
                    currentReconnectionProcess?.dispose();
                    currentReconnectionProcess = null;
                }
            }
        }
    });
})();