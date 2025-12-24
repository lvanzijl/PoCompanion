(async function() {
    if (!('serviceWorker' in navigator)) return;

    try {
        const registrations = await navigator.serviceWorker.getRegistrations();
        if (registrations && registrations.length) {
            for (const reg of registrations) {
                try {
                    await reg.unregister();
                    console.log('Unregistered service worker:', reg);
                } catch (e) {
                    console.warn('Failed to unregister service worker', e);
                }
            }
        }

        // Clear caches that may hold old app assets
        if ('caches' in window) {
            const keys = await caches.keys();
            await Promise.all(keys.map(k => caches.delete(k)));
            console.log('Cleared caches:', keys);
        }
    } catch (err) {
        console.error('Error while unregistering service workers:', err);
    }
})();
