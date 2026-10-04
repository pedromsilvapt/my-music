import {Alert} from 'react-native';
import {hasAllFilesAccess, requestAllFilesAccess} from '../../modules/repo-files';

export const MISSING_ALL_FILES_ACCESS_MESSAGE =
    'MyMusic needs the "All files access" permission to update the music folder. Grant it in the settings and sync again.';

/**
 * Checks that the app can write to the music repository, which needs Android's "All files access".
 * When it is missing, offers to open the system screen that grants it and returns false: the grant
 * happens outside the app, so the caller has to be retried by the user.
 */
export async function ensureRepositoryWriteAccess(): Promise<boolean> {
    if (await hasAllFilesAccess()) {
        return true;
    }

    Alert.alert('Permission Required', MISSING_ALL_FILES_ACCESS_MESSAGE, [
        {text: 'Cancel', style: 'cancel'},
        {text: 'Open settings', onPress: () => void requestAllFilesAccess()},
    ]);

    return false;
}
