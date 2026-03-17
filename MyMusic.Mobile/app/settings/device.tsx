import {Ionicons} from '@expo/vector-icons';
import {zodResolver} from '@hookform/resolvers/zod';
import {pickDirectory} from '@react-native-documents/picker';
import {useRouter} from 'expo-router';
import * as SecureStore from 'expo-secure-store';
import React, {useState} from 'react';
import {ActivityIndicator, Alert, Modal, ScrollView, StyleSheet, Switch, Text, TouchableOpacity, View} from 'react-native';
import {Controller, useForm} from 'react-hook-form';
import {z} from 'zod';
import {testConnection, type ConnectionTestResult} from '../../src/api/client';
import {createDevice, getDevices, updateDevice} from '../../src/api/devices';
import {Button, Card, ErrorDisplay, Input} from '../../src/components/ui';
import type {ErrorDetails} from '../../src/components/ui/ErrorDisplay';
import {DEVICE_TYPES, getDeviceTypeById, getDeviceTypeIdByLabel} from '../../src/constants/deviceIcons';
import {useTheme} from '../../src/hooks/useTheme';
import {
    getDeviceIcon,
    getDeviceName,
    getImportOnPurchase,
    getNamingTemplate,
    getRepositoryPath,
    getServerUrl,
    getUserName,
    setDeviceIcon,
    setDeviceId,
    setDeviceName,
    setImportOnPurchase,
    setIsConfigured,
    setLastSyncAt,
    setNamingTemplate,
    setRepositoryPath,
    setServerUrl,
    setUserName
} from '../../src/services/configService';

const configSchema = z.object({
    serverUrl: z.string().url('Invalid URL').or(z.string().startsWith('http://') || z.string().startsWith('https://')),
    userName: z.string().optional(),
    deviceName: z.string().min(1, 'Device name is required'),
    deviceType: z.string(),
    namingTemplate: z.string().optional(),
    importOnPurchase: z.boolean(),
    repositoryPath: z.string().optional(),
});

type ConfigFormData = z.infer<typeof configSchema>;

export default function DeviceConfigScreen() {
    const router = useRouter();
    const {colors, fontSize, fontWeight, spacing, borderRadius, withAlpha} = useTheme();
    const [saving, setSaving] = useState(false);
    const [testingConnection, setTestingConnection] = useState(false);
    const [connectionStatus, setConnectionStatus] = useState<'idle' | 'success' | 'error'>('idle');
    const [step, setStep] = useState<'form' | 'registering' | 'done'>('form');
    const [connectionError, setConnectionError] = useState<ErrorDetails | null>(null);

    const {control, handleSubmit, watch, setValue, formState: {errors}} = useForm<ConfigFormData>({
        resolver: zodResolver(configSchema),
        defaultValues: {
            serverUrl: getServerUrl() || 'http://localhost:5000/api',
            userName: getUserName() || '',
            deviceName: getDeviceName() || 'My Phone',
            deviceType: getDeviceTypeById(getDeviceIcon())?.label || 'Smartphone',
            namingTemplate: getNamingTemplate() || '',
            importOnPurchase: getImportOnPurchase(),
            repositoryPath: getRepositoryPath() || '',
        },
    });

    const selectedType = watch('deviceType');
    const importOnPurchaseValue = watch('importOnPurchase');

    const handleTestConnection = async (serverUrlValue: string) => {
        if (!serverUrlValue) {
            Alert.alert('Error', 'Please enter a server URL first');
            return;
        }

        setTestingConnection(true);
        setConnectionStatus('idle');
        setConnectionError(null);

        try {
            const apiUrl = serverUrlValue.endsWith('/api') ? serverUrlValue : `${serverUrlValue}/api`;
            const result: ConnectionTestResult = await testConnection(apiUrl);

            setConnectionStatus(result.success ? 'success' : 'error');

            if (result.success) {
                Alert.alert('Success', result.message);
            } else {
                const errorDetails: ErrorDetails = {
                    title: 'Connection Failed',
                    status: result.status,
                    message: result.message,
                    url: result.url,
                    responseBody: result.responseBody,
                    stack: result.stack,
                };
                setConnectionError(errorDetails);
            }
        } catch (error: any) {
            setConnectionStatus('error');
            setConnectionError({
                title: 'Connection Error',
                message: error.message || 'Failed to connect to server',
                stack: error.stack,
            });
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
            await setNamingTemplate(data.namingTemplate || '');
            await setImportOnPurchase(data.importOnPurchase);
            await setRepositoryPath(data.repositoryPath || '');

            const apiServerUrl = data.serverUrl.endsWith('/api') ? data.serverUrl : `${data.serverUrl}/api`;
            await setServerUrl(apiServerUrl);

            try {
                const devicesResponse = await getDevices();
                const existingDevice = devicesResponse.devices.find(d => d.name === data.deviceName);

                if (existingDevice) {
                    await setDeviceId(existingDevice.id);
                    if (existingDevice.namingTemplate !== data.namingTemplate) {
                        await updateDevice(existingDevice.id, {
                            namingTemplate: data.namingTemplate || undefined,
                        });
                    }
                } else {
                    const newDevice = await createDevice({
                        name: data.deviceName,
                        icon: getDeviceTypeIdByLabel(data.deviceType),
                        namingTemplate: data.namingTemplate || undefined,
                        importOnPurchase: data.importOnPurchase,
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
            <View style={[styles.loadingContainer, {backgroundColor: colors.backgroundSecondary, gap: spacing.md}]}>
                <ActivityIndicator size="large" color={colors.primary}/>
                <Text style={[styles.loadingText, {fontSize: fontSize.lg, color: colors.text}]}>Configuring device...</Text>
            </View>
        );
    }

    if (step === 'done') {
        return (
            <View style={[styles.loadingContainer, {backgroundColor: colors.backgroundSecondary, gap: spacing.md}]}>
                <Ionicons name="checkmark-circle" size={64} color={colors.success}/>
                <Text style={[styles.loadingText, {fontSize: fontSize.lg, color: colors.text}]}>Configuration saved!</Text>
            </View>
        );
    }

    return (
        <ScrollView style={[styles.container, {backgroundColor: colors.backgroundSecondary}]} contentContainerStyle={[styles.content, {padding: spacing.md, paddingBottom: spacing.xxl}]}>
            <Card>
                <Text style={[styles.sectionTitle, {fontSize: fontSize.lg, fontWeight: fontWeight.semibold, color: colors.cardText, marginBottom: spacing.md}]}>Server</Text>
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
                            variant="card"
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
                            variant="card"
                        />
                    )}
                />

                <View style={[styles.testConnectionRow, {gap: spacing.sm, marginTop: spacing.sm}]}>
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
                <Text style={[styles.sectionTitle, {fontSize: fontSize.lg, fontWeight: fontWeight.semibold, color: colors.cardText, marginBottom: spacing.md}]}>Device</Text>

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
                            variant="card"
                        />
                    )}
                />

                <Text style={[styles.label, {fontSize: fontSize.sm, fontWeight: fontWeight.medium, color: colors.cardTextSecondary, marginBottom: spacing.xs}]}>Device Type</Text>
                <View style={[styles.deviceTypeGrid, {gap: spacing.sm}]}>
                    {DEVICE_TYPES.map((type) => (
                        <TouchableOpacity
                            key={type.id}
                            style={[
                                styles.deviceTypeButton,
                                {
                                    backgroundColor: colors.cardSecondary,
                                    borderRadius: borderRadius.md,
                                    padding: spacing.md,
                                    borderWidth: 1,
                                        borderColor: colors.cardBorder,
                                },
                                selectedType === type.label && {
                                    borderColor: colors.primary,
                                    backgroundColor: withAlpha('primary', 0.12),
                                },
                            ]}
                            onPress={() => setValue('deviceType', type.label)}
                        >
                            <Ionicons
                                name={type.icon as any}
                                size={24}
                                color={selectedType === type.label ? colors.primary : colors.cardTextSecondary}
                            />
                            <Text style={[
                                styles.deviceTypeText,
                                {fontSize: fontSize.xs, color: colors.cardTextSecondary, marginTop: spacing.xs},
                                selectedType === type.label && {color: colors.primary, fontWeight: fontWeight.medium},
                            ]}>
                                {type.label}
                            </Text>
                        </TouchableOpacity>
                    ))}
                </View>

                <Controller
                    control={control}
                    name="namingTemplate"
                    render={({field: {onChange, onBlur, value}}) => (
                        <View style={[styles.namingTemplateContainer, {marginTop: spacing.md}]}>
                            <Input
                                label="Naming Template"
                                placeholder='{{ album.artist.name }}/{{ album.name }}/{{ simple_label }}.mp3'
                                value={value}
                                onChangeText={onChange}
                                onBlur={onBlur}
                                variant="card"
                            />
                            <Text style={[styles.hint, {fontSize: fontSize.sm, color: colors.cardTextMuted, marginTop: spacing.xs}]}>
                                Template for downloaded file names. Variables: {'{{ album.artist.name }}'}, {'{{ album.name }}'}, {'{{ title }}'}, {'{{ artists }}'}, {'{{ track }}'}, {'{{ year }}'}, {'{{ simple_label }}'}, {'{{ full_label }}'}
                            </Text>
                        </View>
                    )}
                />

                <Controller
                    control={control}
                    name="importOnPurchase"
                    render={({field: {onChange, value}}) => (
                        <View style={[styles.toggleRow, {marginTop: spacing.lg, paddingTop: spacing.md, borderTopWidth: 1, borderTopColor: colors.cardBorder}]}>
                            <View style={[styles.toggleLabelContainer, {marginRight: spacing.md}]}>
                                <Text style={[styles.toggleLabel, {fontSize: fontSize.md, fontWeight: fontWeight.medium, color: colors.cardText}]}>Auto-import on purchase</Text>
                                <Text style={[styles.toggleHint, {fontSize: fontSize.sm, color: colors.cardTextMuted, marginTop: spacing.xs}]}>
                                    Automatically download purchased songs to this device
                                </Text>
                            </View>
                            <Switch
                                value={value}
                                onValueChange={onChange}
                                trackColor={{false: colors.cardBorder, true: colors.primary}}
                                thumbColor={colors.cardText}
                            />
                        </View>
                    )}
                />
            </Card>

            <Card>
                <Text style={[styles.sectionTitle, {fontSize: fontSize.lg, fontWeight: fontWeight.semibold, color: colors.cardText, marginBottom: spacing.md}]}>Repository</Text>

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
                                variant="card"
                            />
                            <TouchableOpacity 
                                style={[
                                    styles.browseButton,
                                    {
                                        borderRadius: borderRadius.md,
                                        marginTop: -spacing.sm,
                                    },
                                    {backgroundColor: withAlpha('primary', 0.08), borderWidth: 1, borderColor: withAlpha('primary', 0.18)}
                                ]} 
                                onPress={handlePickFolder}
                            >
                                <Ionicons name="folder-open-outline" size={20} color={colors.primary}/>
                                <Text style={[styles.browseButtonText, {color: colors.primary, fontWeight: fontWeight.medium, fontSize: fontSize.sm}]}>Browse</Text>
                            </TouchableOpacity>
                        </View>
                    )}
                />

                <Text style={[styles.hint, {fontSize: fontSize.sm, color: colors.cardTextMuted, marginTop: spacing.xs}]}>
                    Tap "Browse" to select a folder from your device, or enter the path manually.
                </Text>
            </Card>

            <View style={[styles.actions, {marginTop: spacing.lg}]}>
                <Button
                    title="Save Configuration"
                    onPress={handleSubmit(onSubmit)}
                    loading={saving}
                    size="large"
                />
            </View>

            <Modal
                visible={connectionError !== null}
                transparent
                animationType="fade"
                onRequestClose={() => setConnectionError(null)}
            >
                <View style={[styles.modalOverlay, {backgroundColor: 'rgba(0, 0, 0, 0.7)'}]}>
                    <View style={[styles.modalContent, {backgroundColor: colors.surfaceSecondary, borderRadius: borderRadius.lg, padding: spacing.sm}]}>
                        {connectionError && (
                            <ErrorDisplay
                                error={connectionError}
                                onDismiss={() => setConnectionError(null)}
                            />
                        )}
                    </View>
                </View>
            </Modal>
        </ScrollView>
    );
}

const styles = StyleSheet.create({
    container: {
        flex: 1,
    },
    content: {
        padding: 16,
        paddingBottom: 48,
    },
    loadingContainer: {
        flex: 1,
        justifyContent: 'center',
        alignItems: 'center',
    },
    loadingText: {
        fontSize: 16,
    },
    sectionTitle: {
        fontSize: 16,
        marginBottom: 16,
    },
    label: {
        fontSize: 12,
        marginBottom: 4,
    },
    deviceTypeGrid: {
        flexDirection: 'row',
        flexWrap: 'wrap',
    },
    deviceTypeButton: {
        width: '31%',
        alignItems: 'center',
    },
    deviceTypeText: {
        fontSize: 10,
        textAlign: 'center',
    },
    hint: {
        fontSize: 12,
    },
    testConnectionRow: {
        flexDirection: 'row',
        alignItems: 'center',
    },
    browseButton: {
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'center',
        paddingVertical: 8,
        paddingHorizontal: 16,
        marginTop: -8,
    },
    browseButtonText: {
        fontSize: 12,
    },
    toggleRow: {
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'space-between',
        borderTopWidth: 1,
    },
    toggleLabelContainer: {
        flex: 1,
    },
    toggleLabel: {
        fontSize: 14,
    },
    toggleHint: {
        fontSize: 12,
    },
    namingTemplateContainer: {
        marginTop: 16,
    },
    toggle: {
        width: 50,
        height: 30,
        borderRadius: 15,
    },
    toggleThumb: {
        width: 26,
        height: 26,
        borderRadius: 13,
    },
    actions: {
        marginTop: 24,
    },
    modalOverlay: {
        flex: 1,
        justifyContent: 'center',
        alignItems: 'center',
        padding: 16,
    },
    modalContent: {
        width: '95%',
        maxWidth: 400,
        maxHeight: '80%',
        minHeight: 200,
    },
});
