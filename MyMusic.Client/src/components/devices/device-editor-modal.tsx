import {Alert, Button, CloseButton, ColorInput, DEFAULT_THEME, Group, Select, Stack, Switch, TextInput} from "@mantine/core";
import {type ContextModalProps, modals} from "@mantine/modals";
import {type FormEvent, useEffect, useMemo, useState} from "react";
import {useTranslation} from "react-i18next";
import {usePostDevices, usePutDevicesDeviceId} from "../../client/devices.ts";
import {ApiProblemError, throwOnProblem} from "../../utils/api-problem.ts";
import TablerIcon from "../common/tabler-icon.tsx";
import {DEFAULT_DEVICE_ICON, DEVICE_ICONS} from "./device-icons.ts";
import NamingTemplateField from "./naming-template-field.tsx";

export interface EditableDevice {
    id: number;
    name: string;
    icon?: string | null;
    color?: string | null;
    namingTemplate?: string | null;
    importOnPurchase?: boolean;
}

export interface DeviceEditorModalInnerProps {
    /** The device to edit. Without it, a new device is created. */
    device?: EditableDevice;
    /** Called with the id of the saved device, after the dialog closes. */
    onSuccess?: (deviceId: number) => void;
}

interface DeviceForm {
    name: string;
    icon: string | null;
    color: string;
    namingTemplate: string;
    importOnPurchase: boolean;
}

const COLOR_SWATCHES = ["red", "pink", "grape", "violet", "indigo", "blue", "cyan", "teal", "green", "lime", "yellow", "orange"]
    .map(color => DEFAULT_THEME.colors[color]![6]);

/**
 * Creates a device or edits every option of an existing one. The dialog stays open until the server is
 * done, and shows why when it refuses (e.g. there is already a device with that name).
 */
export default function DeviceEditorModal({context, id, innerProps}: ContextModalProps<DeviceEditorModalInnerProps>) {
    const {t} = useTranslation(["devices", "common"]);
    const {device, onSuccess} = innerProps;
    const [form, setFormState] = useState<DeviceForm>(() => ({
        name: device?.name ?? "",
        icon: device?.icon ?? null,
        color: device?.color ?? "",
        namingTemplate: device?.namingTemplate ?? "",
        importOnPurchase: device?.importOnPurchase ?? false,
    }));
    const [templateValid, setTemplateValid] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [saving, setSaving] = useState(false);
    const {mutateAsync: createDevice} = usePostDevices();
    const {mutateAsync: updateDevice} = usePutDevicesDeviceId();

    // The dialog cannot be dismissed while the device is being saved
    useEffect(() => {
        modals.updateContextModal({
            modalId: id,
            closeOnEscape: !saving,
            closeOnClickOutside: !saving,
            withCloseButton: !saving,
        });
    }, [id, saving]);

    const setForm = (changes: Partial<DeviceForm>) => setFormState(form => ({...form, ...changes}));

    const iconOptions = useMemo(() => {
        const options: { value: string; label: string }[] = DEVICE_ICONS.map(({icon, key}) => ({
            value: icon,
            label: t(`devices:icons.${key}`),
        }));

        // An icon set by other means is kept, shown by its name
        if (device?.icon && !options.some(option => option.value === device.icon)) {
            options.push({value: device.icon, label: device.icon});
        }

        return options;
    }, [device?.icon, t]);

    const canSave = form.name.trim() !== "" && templateValid;

    const handleSubmit = async (event: FormEvent) => {
        event.preventDefault();

        if (!canSave || saving) {
            return;
        }

        const data = {
            name: form.name.trim(),
            icon: form.icon,
            color: form.color || null,
            namingTemplate: form.namingTemplate.trim() === "" ? null : form.namingTemplate,
            importOnPurchase: form.importOnPurchase,
        };

        setError(null);
        setSaving(true);
        let deviceId: number;
        try {
            const response = device
                ? throwOnProblem(await updateDevice({deviceId: device.id, data}))
                : throwOnProblem(await createDevice({data}));
            deviceId = response.data.device.id;
        } catch (error) {
            setSaving(false);
            // The server explains why it refused (e.g. there is already a device with that name)
            setError((error instanceof ApiProblemError ? error.detail : undefined) ?? t("devices:editModal.saveFailed"));
            return;
        }

        context.closeModal(id);
        onSuccess?.(deviceId);
    };

    return (
        <form onSubmit={handleSubmit} data-testid="device-editor">
            <Stack>
                <TextInput
                    label={t("devices:editModal.nameLabel")}
                    description={t("devices:editModal.nameDescription")}
                    value={form.name}
                    onChange={event => setForm({name: event.target.value})}
                    disabled={saving}
                    maxLength={256}
                    required
                    data-autofocus
                    data-testid="device-editor-name"
                />
                <Group grow align="flex-start">
                    <Select
                        label={t("devices:editModal.iconLabel")}
                        placeholder={t("devices:editModal.iconPlaceholder")}
                        data={iconOptions}
                        value={form.icon}
                        onChange={icon => setForm({icon})}
                        leftSection={<TablerIcon icon={form.icon} defaultIcon={DEFAULT_DEVICE_ICON} size={18} color={form.color || "gray"}/>}
                        renderOption={({option}) => (
                            <Group gap="xs" wrap="nowrap" data-testid="device-editor-icon-option" data-icon={option.value}>
                                <TablerIcon icon={option.value} defaultIcon={DEFAULT_DEVICE_ICON} size={18}/>
                                {option.label}
                            </Group>
                        )}
                        clearable
                        disabled={saving}
                        data-testid="device-editor-icon"
                    />
                    <ColorInput
                        label={t("devices:editModal.colorLabel")}
                        placeholder={t("devices:editModal.colorPlaceholder")}
                        format="hex"
                        swatches={COLOR_SWATCHES}
                        swatchesPerRow={6}
                        value={form.color}
                        onChange={color => setForm({color})}
                        withEyeDropper={false}
                        rightSection={form.color && !saving
                            ? <CloseButton
                                size="sm"
                                aria-label={t("devices:editModal.clearColor")}
                                onClick={() => setForm({color: ""})}
                                data-testid="device-editor-color-clear"
                            />
                            : undefined}
                        disabled={saving}
                        data-testid="device-editor-color"
                    />
                </Group>
                <Switch
                    label={t("devices:editModal.importOnPurchaseLabel")}
                    description={t("devices:editModal.importOnPurchaseDescription")}
                    checked={form.importOnPurchase}
                    onChange={event => setForm({importOnPurchase: event.currentTarget.checked})}
                    disabled={saving}
                    data-testid="device-editor-import-on-purchase"
                />
                <NamingTemplateField
                    deviceId={device?.id}
                    value={form.namingTemplate}
                    onChange={namingTemplate => setForm({namingTemplate})}
                    onValidityChange={setTemplateValid}
                    disabled={saving}
                />
                {error && (
                    <Alert color="red" data-testid="device-editor-error">
                        {error}
                    </Alert>
                )}
                <Group justify="flex-end">
                    <Button variant="subtle" onClick={() => context.closeModal(id)} disabled={saving}>
                        {t("common:actions.cancel")}
                    </Button>
                    <Button type="submit" loading={saving} disabled={!canSave} data-testid="device-editor-submit">
                        {t("common:actions.save")}
                    </Button>
                </Group>
            </Stack>
        </form>
    );
}
