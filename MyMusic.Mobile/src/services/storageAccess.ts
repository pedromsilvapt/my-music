import {Alert, AppState} from 'react-native';
import {hasAllFilesAccess, requestAllFilesAccess} from '../../modules/repo-files';

export const MISSING_ALL_FILES_ACCESS_MESSAGE =
    'MyMusic needs the "All files access" permission to read and update the music folder. Grant it in the settings and sync again.';

/**
 * Checks that the app can read and write the music repository, which needs Android's "All files access".
 * When it is missing, offers to open the system screen that grants it, waits for the user to come back
 * to the app and checks again: only then, or when the user declines, does it return false.
 */
export async function ensureRepositoryAccess(): Promise<boolean> {
    if (await hasAllFilesAccess()) {
        return true;
    }

    if (!(await askToOpenSettings())) {
        return false;
    }

    // The grant happens outside the app, so listen before leaving it
    const returned = waitForReturnToApp();
    try {
        await requestAllFilesAccess();
        await returned.promise;
    } catch (error) {
        console.error('Error opening the All files access settings:', error);
        return false;
    } finally {
        returned.cancel();
    }

    return hasAllFilesAccess();
}

function askToOpenSettings(): Promise<boolean> {
    return new Promise((resolve) => {
        Alert.alert(
            'Permission Required',
            MISSING_ALL_FILES_ACCESS_MESSAGE,
            [
                {text: 'Cancel', style: 'cancel', onPress: () => resolve(false)},
                {text: 'Open settings', onPress: () => resolve(true)},
            ],
            {cancelable: true, onDismiss: () => resolve(false)},
        );
    });
}

/** Resolves when the app is active again after having been left. */
function waitForReturnToApp(): {promise: Promise<void>; cancel: () => void} {
    let cancel = () => {};

    const promise = new Promise<void>((resolve) => {
        let left = false;
        const subscription = AppState.addEventListener('change', (state) => {
            if (state !== 'active') {
                left = true;
            } else if (left) {
                subscription.remove();
                resolve();
            }
        });
        cancel = () => subscription.remove();
    });

    return {promise, cancel};
}
