import {Alert} from 'react-native';
import {hasAllFilesAccess, requestAllFilesAccess} from '../../../modules/repo-files';
import {ensureRepositoryWriteAccess} from '../storageAccess';

jest.mock('react-native', () => ({Alert: {alert: jest.fn()}}));
jest.mock('../../../modules/repo-files', () => ({
    hasAllFilesAccess: jest.fn(),
    requestAllFilesAccess: jest.fn(),
}));

const hasAccess = hasAllFilesAccess as jest.Mock;
const alert = Alert.alert as jest.Mock;

describe('ensureRepositoryWriteAccess', () => {
    beforeEach(() => jest.clearAllMocks());

    test('does not prompt when All files access is granted', async () => {
        hasAccess.mockResolvedValue(true);

        expect(await ensureRepositoryWriteAccess()).toBe(true);
        expect(alert).not.toHaveBeenCalled();
    });

    test('offers to open the settings and reports no access when the permission is missing', async () => {
        hasAccess.mockResolvedValue(false);

        expect(await ensureRepositoryWriteAccess()).toBe(false);

        // The settings screen only opens when the user asks for it
        expect(requestAllFilesAccess).not.toHaveBeenCalled();
        const buttons = alert.mock.calls[0][2] as {text: string; onPress?: () => void}[];
        buttons.find((button) => button.text === 'Open settings')!.onPress!();
        expect(requestAllFilesAccess).toHaveBeenCalled();
    });
});
