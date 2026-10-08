import {getDevices, updateDevice} from '../../api/devices';
import {getDeviceName, setDeviceIcon, setDeviceId} from '../configService';
import {DeviceNotFoundError, findDevice, loadDeviceOptions, saveDeviceOptions} from '../deviceConfigService';

jest.mock('../../api/devices', () => ({getDevices: jest.fn(), updateDevice: jest.fn()}));
jest.mock('../configService', () => ({
    getDeviceName: jest.fn(),
    setDeviceIcon: jest.fn(),
    setDeviceId: jest.fn(),
}));

const serverDevice = {
    id: 7,
    name: 'My Phone',
    icon: 'IconDeviceTablet',
    color: '#10B981',
    namingTemplate: '{{ year }}/{{ simple_label }}.mp3',
    importOnPurchase: true,
    songCount: 3,
};

describe('deviceConfigService', () => {
    beforeEach(() => {
        jest.clearAllMocks();
        (getDeviceName as jest.Mock).mockReturnValue('My Phone');
        (getDevices as jest.Mock).mockResolvedValue({
            devices: [{...serverDevice, id: 3, name: 'Laptop'}, serverDevice],
        });
        (updateDevice as jest.Mock).mockResolvedValue({});
    });

    describe('findDevice', () => {
        test('finds the device with the configured name and stores its id and icon', async () => {
            const device = await findDevice();

            expect(device.id).toBe(7);
            expect(setDeviceId).toHaveBeenCalledWith(7);
            expect(setDeviceIcon).toHaveBeenCalledWith('IconDeviceTablet');
        });

        test('keeps the stored icon of a device that has none', async () => {
            (getDevices as jest.Mock).mockResolvedValue({devices: [{...serverDevice, icon: null}]});

            await findDevice();

            expect(setDeviceIcon).not.toHaveBeenCalled();
        });

        test('fails when no device has the configured name, telling to create it in the web app', async () => {
            (getDeviceName as jest.Mock).mockReturnValue('Unknown');

            await expect(findDevice()).rejects.toThrow(DeviceNotFoundError);
            await expect(findDevice()).rejects.toThrow(
                "Device 'Unknown' not found. Create it in the web app (Devices > New device)");
            expect(setDeviceId).not.toHaveBeenCalled();
        });

        test('never creates or changes a device', async () => {
            (getDeviceName as jest.Mock).mockReturnValue('Unknown');

            await expect(findDevice()).rejects.toThrow();

            expect(updateDevice).not.toHaveBeenCalled();
        });
    });

    describe('loadDeviceOptions', () => {
        test('returns the options of the server device', async () => {
            await expect(loadDeviceOptions()).resolves.toEqual({
                deviceId: 7,
                icon: 'IconDeviceTablet',
                color: '#10B981',
                namingTemplate: '{{ year }}/{{ simple_label }}.mp3',
                importOnPurchase: true,
            });
        });
    });

    describe('saveDeviceOptions', () => {
        test('saves the options, sending back the color they were loaded with', async () => {
            const loaded = await loadDeviceOptions();
            jest.clearAllMocks();

            await saveDeviceOptions(loaded, {icon: 'IconDeviceMobile', namingTemplate: '{{ title }}.mp3', importOnPurchase: false});

            expect(updateDevice).toHaveBeenCalledWith(7, {
                icon: 'IconDeviceMobile',
                color: '#10B981',
                namingTemplate: '{{ title }}.mp3',
                importOnPurchase: false,
            });
            expect(setDeviceIcon).toHaveBeenCalledWith('IconDeviceMobile');
        });

        test('a cleared naming template is left out, which resets it to the default', async () => {
            const loaded = await loadDeviceOptions();

            await saveDeviceOptions(loaded, {icon: 'IconDeviceMobile', namingTemplate: null, importOnPurchase: false});

            expect((updateDevice as jest.Mock).mock.calls[0][1].namingTemplate).toBeUndefined();
        });
    });
});
