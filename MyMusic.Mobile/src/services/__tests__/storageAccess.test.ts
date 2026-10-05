import {Alert, AppState} from 'react-native';
import {hasAllFilesAccess, requestAllFilesAccess} from '../../../modules/repo-files';
import {ensureRepositoryAccess} from '../storageAccess';

jest.mock('react-native', () => ({
    Alert: {alert: jest.fn()},
    AppState: {addEventListener: jest.fn()},
}));
jest.mock('../../../modules/repo-files', () => ({
    hasAllFilesAccess: jest.fn(),
    requestAllFilesAccess: jest.fn(),
}));

const hasAccess = hasAllFilesAccess as jest.Mock;
const requestAccess = requestAllFilesAccess as jest.Mock;
const alert = Alert.alert as jest.Mock;
const addAppStateListener = AppState.addEventListener as jest.Mock;
const removeAppStateListener = jest.fn();

/** Lets the pending promise callbacks of the code under test run. */
const settle = () => new Promise((resolve) => setImmediate(resolve));

function pressAlertButton(text: string) {
    const buttons = alert.mock.calls[0][2] as {text: string; onPress?: () => void}[];
    buttons.find((button) => button.text === text)!.onPress!();
}

function changeAppState(state: string) {
    addAppStateListener.mock.calls[0][1](state);
}

describe('ensureRepositoryAccess', () => {
    beforeEach(() => {
        jest.clearAllMocks();
        jest.spyOn(console, 'error').mockImplementation(() => {});
        addAppStateListener.mockReturnValue({remove: removeAppStateListener});
        requestAccess.mockResolvedValue(undefined);
    });

    test('does not prompt when All files access is granted', async () => {
        hasAccess.mockResolvedValue(true);

        expect(await ensureRepositoryAccess()).toBe(true);
        expect(alert).not.toHaveBeenCalled();
    });

    test('reports no access when the user declines to open the settings', async () => {
        hasAccess.mockResolvedValue(false);

        const access = ensureRepositoryAccess();
        await settle();
        pressAlertButton('Cancel');

        expect(await access).toBe(false);
        // The settings screen only opens when the user asks for it
        expect(requestAccess).not.toHaveBeenCalled();
    });

    test('reports no access when the prompt is dismissed', async () => {
        hasAccess.mockResolvedValue(false);

        const access = ensureRepositoryAccess();
        await settle();
        alert.mock.calls[0][3].onDismiss();

        expect(await access).toBe(false);
    });

    test('waits for the user to come back from the settings and checks again', async () => {
        hasAccess.mockResolvedValueOnce(false).mockResolvedValueOnce(true);
        let done = false;

        // Missing permission: the user is sent to the settings screen
        const access = ensureRepositoryAccess().finally(() => {
            done = true;
        });
        await settle();
        pressAlertButton('Open settings');
        await settle();
        expect(requestAccess).toHaveBeenCalled();

        // Nothing is decided while the user is away from the app
        changeAppState('background');
        await settle();
        expect(done).toBe(false);
        expect(hasAccess).toHaveBeenCalledTimes(1);

        // Back in the app: the second check decides
        changeAppState('active');

        expect(await access).toBe(true);
        expect(hasAccess).toHaveBeenCalledTimes(2);
        expect(removeAppStateListener).toHaveBeenCalled();
    });

    test('reports no access when the user comes back without granting it', async () => {
        hasAccess.mockResolvedValue(false);

        const access = ensureRepositoryAccess();
        await settle();
        pressAlertButton('Open settings');
        await settle();
        changeAppState('background');
        changeAppState('active');

        expect(await access).toBe(false);
        expect(hasAccess).toHaveBeenCalledTimes(2);
    });

    test('reports no access when the settings screen cannot be opened', async () => {
        hasAccess.mockResolvedValue(false);
        requestAccess.mockRejectedValue(new Error('No activity found'));

        const access = ensureRepositoryAccess();
        await settle();
        pressAlertButton('Open settings');

        expect(await access).toBe(false);
        expect(removeAppStateListener).toHaveBeenCalled();
    });
});
