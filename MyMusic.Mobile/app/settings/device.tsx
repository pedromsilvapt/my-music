import {Ionicons} from '@expo/vector-icons';
import {zodResolver} from '@hookform/resolvers/zod';
import {pickDirectory} from '@react-native-documents/picker';
import {useRouter} from 'expo-router';
import * as SecureStore from 'expo-secure-store';
import React, {useState} from 'react';
import {Controller, useForm} from 'react-hook-form';
import {ActivityIndicator, Alert, ScrollView, StyleSheet, Text, TouchableOpacity, View} from 'react-native';
import {z} from 'zod';
import {testConnection} from '../../src/api/client';
import {createDevice, getDevices} from '../../src/api/devices';
import {Button, Card, Input} from '../../src/components/ui';
import {DEVICE_TYPES, getDeviceTypeById, getDeviceTypeIdByLabel} from '../../src/constants/deviceIcons';
import {borderRadius, colors, fontSize, fontWeight, spacing} from '../../src/constants/theme';
import {
    getDeviceIcon,
    getDeviceName,
    getRepositoryPath,
    getServerUrl,
    getUserName,
    setDeviceIcon,
    setDeviceId,
    setDeviceName,
    setIsConfigured,
    setLastSyncAt,
    setRepositoryPath,
    setServerUrl,
    setUserName
} from '../../src/services/configService';

const configSchema = z.object({
    serverUrl: z.string().url('Invalid URL').or(z.string().startsWith('http://') || z.string().startsWith('https://')),
    userName: z.string().optional(),
    deviceName: z.string().min(1, 'Device name is required'),
    deviceType: z.string(),
    repositoryPath: z.string().optional(),
});

type ConfigFormData = z.infer<typeof configSchema>;

export default function DeviceConfigScreen() {
    const router = useRouter();
    const [saving, setSaving] = useState(false);
    const [testingConnection, setTestingConnection] = useState(false);
    const [connectionStatus, setConnectionStatus] = useState<'idle' | 'success' | 'error'>('idle');
    const [step, setStep] = useState<'form' | 'registering' | 'done'>('form');

    const {control, handleSubmit, watch, setValue, formState: {errors}} = useForm<ConfigFormData>({
        resolver: zodResolver(configSchema),
        defaultValues: {
            serverUrl: getServerUrl() || 'http://localhost:5000/api',
            userName: getUserName() || '',
            deviceName: getDeviceName() || 'My Phone',
            deviceType: getDeviceTypeById(getDeviceIcon())?.label || 'Smartphone',
            repositoryPath: getRepositoryPath() || '',
        },
    });

    const selectedType = watch('deviceType');

    const handleTestConnection = async (serverUrlValue: string) => {
        if (!serverUrlValue) {
            Alert.alert('Error', 'Please enter a server URL first');
            return;
        }

        setTestingConnection(true);
        setConnectionStatus('idle');

        try {
            const apiUrl = serverUrlValue.endsWith('/api') ? serverUrlValue : `${serverUrlValue}/api`;
            const result = await testConnection(apiUrl);

            setConnectionStatus(result.success ? 'success' : 'error');

            if (result.success) {
                Alert.alert('Success', result.message);
            } else {
                Alert.alert('Connection Failed', result.message);
            }
        } catch (error: any) {
            setConnectionStatus('error');
            Alert.alert('Connection Error', error.message || 'Failed to connect to server');
        } finally {
            setTestingConnection(false);
        }
    };

    const handlePickFolder = async () => {
        try {
            const {uri} = await pickDirectory({
                requestLongTermAccess: true,
            });

            if (uri) {
                setValue('repositoryPath', uri);
            }
        } catch (error: any) {
            console.error('Error picking folder:', error);
            Alert.alert('Error', 'Failed to select folder');
        }
    };

    const onSubmit = async (data: ConfigFormData) => {
        setSaving(true);
        setStep('registering');

        try {
            const baseUrl = data.serverUrl.replace('/api', '');
            await setServerUrl(baseUrl);

            await SecureStore.setItemAsync('userName', data.userName || '');
            await setUserName(data.userName || '');
            await setDeviceName(data.deviceName);
            await setDeviceIcon(getDeviceTypeIdByLabel(data.deviceType));
            await setRepositoryPath(data.repositoryPath || '');

            const apiServerUrl = data.serverUrl.endsWith('/api') ? data.serverUrl : `${data.serverUrl}/api`;
            await setServerUrl(apiServerUrl);

            try {
                const devicesResponse = await getDevices();
                const existingDevice = devicesResponse.devices.find(d => d.name === data.deviceName);

                if (existingDevice) {
                    await setDeviceId(existingDevice.id);
                } else {
                    const newDevice = await createDevice({
                        name: data.deviceName,
                        icon: getDeviceTypeIdByLabel(data.deviceType),
                    });
                    await setDeviceId(newDevice.device.id);
                }
            } catch (apiError) {
                console.error('API error (non-critical):', apiError);
            }

            await setIsConfigured(true);
            await setLastSyncAt(null);
            setStep('done');

            setTimeout(() => {
                router.back();
            }, 1500);

        } catch (error: any) {
            console.error('Failed to save config:', error);
            Alert.alert(
                'Configuration Error',
                error.message || 'Failed to save configuration. Please check your server URL and try again.',
                [{text: 'OK'}]
            );
            setStep('form');
        } finally {
            setSaving(false);
        }
    };

    if (step === 'registering') {
        return (
            <View style={styles.loadingContainer}>
                <ActivityIndicator size="large" color={colors.primary}/>
                <Text style={styles.loadingText}>Configuring device...</Text>
            </View>
        );
    }

    if (step === 'done') {
        return (
            <View style={styles.loadingContainer}>
                <Ionicons name="checkmark-circle" size={64} color={colors.success}/>
                <Text style={styles.loadingText}>Configuration saved!</Text>
            </View>
        );
    }

    return (
        <ScrollView style={styles.container} contentContainerStyle={styles.content}>
            <Card>
                <Text style={styles.sectionTitle}>Server</Text>
                <Controller
                    control={control}
                    name="serverUrl"
                    render={({field: {onChange, onBlur, value}}) => (
                        <Input
                            label="Server URL"
                            placeholder="https://your-server.com/api"
                            value={value}
                            onChangeText={onChange}
                            onBlur={onBlur}
                            error={errors.serverUrl?.message}
                            autoCapitalize="none"
                            keyboardType="url"
                        />
                    )}
                />

                <Controller
                    control={control}
                    name="userName"
                    render={({field: {onChange, onBlur, value}}) => (
                        <Input
                            label="Username (optional)"
                            placeholder="Your name"
                            value={value}
                            onChangeText={onChange}
                            onBlur={onBlur}
                            error={errors.userName?.message}
                        />
                    )}
                />

                <View style={styles.testConnectionRow}>
                    <Button
                        title="Test Connection"
                        onPress={handleSubmit((data) => handleTestConnection(data.serverUrl))}
                        loading={testingConnection}
                        variant="outline"
                        size="small"
                        disabled={testingConnection}
                    />
                    {connectionStatus === 'success' && (
                        <Ionicons name="checkmark-circle" size={20} color={colors.success}/>
                    )}
                    {connectionStatus === 'error' && (
                        <Ionicons name="close-circle" size={20} color={colors.error}/>
                    )}
                </View>
            </Card>

            <Card>
                <Text style={styles.sectionTitle}>Device</Text>

                <Controller
                    control={control}
                    name="deviceName"
                    render={({field: {onChange, onBlur, value}}) => (
                        <Input
                            label="Device Name"
                            placeholder="My Phone"
                            value={value}
                            onChangeText={onChange}
                            onBlur={onBlur}
                            error={errors.deviceName?.message}
                        />
                    )}
                />

                <Text style={styles.label}>Device Type</Text>
                <View style={styles.deviceTypeGrid}>
                    {DEVICE_TYPES.map((type) => (
                        <TouchableOpacity
                            key={type.id}
                            style={[
                                styles.deviceTypeButton,
                                selectedType === type.label && styles.deviceTypeButtonActive,
                            ]}
                            onPress={() => setValue('deviceType', type.label)}
                        >
                            <Ionicons
                                name={type.icon as any}
                                size={24}
                                color={selectedType === type.label ? colors.primary : colors.textSecondary}
                            />
                            <Text style={[
                                styles.deviceTypeText,
                                selectedType === type.label && styles.deviceTypeTextActive,
                            ]}>
                                {type.label}
                            </Text>
                        </TouchableOpacity>
                    ))}
                </View>
            </Card>

            <Card>
                <Text style={styles.sectionTitle}>Repository</Text>

                <Controller
                    control={control}
                    name="repositoryPath"
                    render={({field: {onChange, onBlur, value}}) => (
                        <View>
                            <Input
                                label="Music Folder Path"
                                placeholder="Select your music folder"
                                value={value}
                                onChangeText={onChange}
                                onBlur={onBlur}
                                error={errors.repositoryPath?.message}
                            />
                            <TouchableOpacity style={styles.browseButton} onPress={handlePickFolder}>
                                <Ionicons name="folder-open-outline" size={20} color={colors.primary}/>
                                <Text style={styles.browseButtonText}>Browse</Text>
                            </TouchableOpacity>
                        </View>
                    )}
                />

                <Text style={styles.hint}>
                    Tap "Browse" to select a folder from your device, or enter the path manually.
                </Text>
            </Card>

            <View style={styles.actions}>
                <Button
                    title="Save Configuration"
                    onPress={handleSubmit(onSubmit)}
                    loading={saving}
                    size="large"
                />
            </View>
        </ScrollView>
    );
}

const styles = StyleSheet.create({
    container: {
        flex: 1,
        backgroundColor: colors.backgroundDark,
    },
    content: {
        padding: spacing.md,
        paddingBottom: spacing.xxl,
    },
    loadingContainer: {
        flex: 1,
        justifyContent: 'center',
        alignItems: 'center',
        backgroundColor: colors.backgroundDark,
        gap: spacing.md,
    },
    loadingText: {
        fontSize: fontSize.lg,
        color: colors.text,
    },
    sectionTitle: {
        fontSize: fontSize.lg,
        fontWeight: fontWeight.semibold,
        color: colors.text,
        marginBottom: spacing.md,
    },
    label: {
        fontSize: fontSize.sm,
        fontWeight: fontWeight.medium,
        color: colors.textSecondary,
        marginBottom: spacing.xs,
    },
    deviceTypeGrid: {
        flexDirection: 'row',
        flexWrap: 'wrap',
        gap: spacing.sm,
    },
    deviceTypeButton: {
        width: '31%',
        backgroundColor: colors.surface,
        borderRadius: borderRadius.md,
        padding: spacing.md,
        alignItems: 'center',
        borderWidth: 1,
        borderColor: colors.border,
    },
    deviceTypeButtonActive: {
        borderColor: colors.primary,
        backgroundColor: colors.primary + '20',
    },
    deviceTypeText: {
        fontSize: fontSize.xs,
        color: colors.textSecondary,
        marginTop: spacing.xs,
        textAlign: 'center',
    },
    deviceTypeTextActive: {
        color: colors.primary,
        fontWeight: fontWeight.medium,
    },
    hint: {
        fontSize: fontSize.sm,
        color: colors.textMuted,
        marginTop: spacing.xs,
    },
    testConnectionRow: {
        flexDirection: 'row',
        alignItems: 'center',
        gap: spacing.sm,
        marginTop: spacing.sm,
    },
    browseButton: {
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'center',
        gap: spacing.xs,
        paddingVertical: spacing.sm,
        paddingHorizontal: spacing.md,
        backgroundColor: colors.primary + '15',
        borderRadius: borderRadius.md,
        borderWidth: 1,
        borderColor: colors.primary + '30',
        marginTop: -spacing.sm,
    },
    browseButtonText: {
        color: colors.primary,
        fontWeight: fontWeight.medium,
        fontSize: fontSize.sm,
    },
    actions: {
        marginTop: spacing.lg,
    },
});