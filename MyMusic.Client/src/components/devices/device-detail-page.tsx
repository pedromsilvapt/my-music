import {Box, Button, Code, Group, SimpleGrid, Stack, Text, Title} from "@mantine/core";
import {IconEdit, IconTrash} from "@tabler/icons-react";
import {useNavigate, useParams} from "@tanstack/react-router";
import type {ReactNode} from "react";
import {useTranslation} from "react-i18next";
import {useDeleteDevicesDeviceId, useGetDevice} from "../../client/devices.ts";
import {useConfirmDelete} from "../../hooks/use-confirm-delete.ts";
import {useNamingTemplatePreview} from "../../hooks/use-naming-template-preview.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import {throwOnProblem} from "../../utils/api-problem.ts";
import TablerIcon from "../common/tabler-icon.tsx";
import DeviceBreadcrumbs from "./device-breadcrumbs.tsx";
import {DEFAULT_DEVICE_ICON} from "./device-icons.ts";
import DeviceSessionsCollection from "./device-sessions-collection.tsx";
import {useEditDevice} from "./use-edit-device.ts";

export default function DeviceDetailPage() {
    const {t} = useTranslation(["devices", "common"]);
    const {deviceId} = useParams({from: '/devices/$deviceId/'});
    const deviceIdNum = parseInt(deviceId, 10);

    const deviceQuery = useGetDevice(deviceIdNum, {});
    const deviceResponse = useQueryData(deviceQuery, t("devices:detailPage.fetchFailed"));
    const device = deviceResponse?.data?.device ?? null;

    // Only asked for the server's default template, shown for a device that has none
    const usesDefaultTemplate = device !== null && !device.namingTemplate;
    const {preview: defaultTemplatePreview, isFetching: isFetchingDefaultTemplate} =
        useNamingTemplatePreview(undefined, "", {enabled: usesDefaultTemplate});

    const navigate = useNavigate();
    const confirmDelete = useConfirmDelete();
    const editDevice = useEditDevice();
    const {mutateAsync: deleteDevice} = useDeleteDevicesDeviceId();

    if (!device) {
        return <Box p="md" data-testid="device-detail" data-loading="true">{t("common:common.loading")}</Box>;
    }

    const handleDelete = () => confirmDelete({
        title: t("devices:schema.deleteTitle"),
        children: <Text size="sm">{t("devices:schema.deleteConfirmSingle", {name: device.name})}</Text>,
        onConfirm: async () => throwOnProblem(await deleteDevice({deviceId: device.id})),
        onSuccess: () => navigate({to: '/devices'}),
        errorMessage: t("devices:schema.deleteFailed", {name: device.name}),
    });

    const namingTemplate = usesDefaultTemplate ? defaultTemplatePreview?.defaultNamingTemplate : device.namingTemplate;
    const isFetching = deviceQuery.isFetching || (usesDefaultTemplate && isFetchingDefaultTemplate);

    return (
        <Stack gap="md" data-testid="device-detail" data-loading={isFetching ? "true" : "false"}>
            <DeviceBreadcrumbs deviceId={deviceId} deviceName={device.name}/>

            <Group justify="space-between" align="center">
                <Group gap="sm" wrap="nowrap">
                    <Box
                        display="flex"
                        data-testid="device-icon"
                        data-icon={device.icon ?? ""}
                        data-color={device.color ?? ""}
                    >
                        <TablerIcon icon={device.icon} defaultIcon={DEFAULT_DEVICE_ICON} size={40} color={device.color || 'gray'}/>
                    </Box>
                    <Title order={2} data-testid="device-name">{device.name}</Title>
                </Group>
                <Group gap="xs">
                    <Button
                        leftSection={<IconEdit/>}
                        variant="default"
                        onClick={() => editDevice(device)}
                        data-testid="device-edit"
                    >
                        {t("common:actions.edit")}
                    </Button>
                    <Button
                        leftSection={<IconTrash/>}
                        variant="default"
                        color="red"
                        onClick={handleDelete}
                        data-testid="device-delete"
                    >
                        {t("common:actions.delete")}
                    </Button>
                </Group>
            </Group>

            <Stack gap="xs">
                <Title order={4}>{t("devices:detailPage.configuration")}</Title>
                <SimpleGrid cols={{base: 1, sm: 3}}>
                    <Summary label={t("devices:detailPage.songs")}>
                        <Text data-testid="device-song-count" data-count={device.songCount}>
                            {t("common:count.songs", {count: device.songCount})}
                        </Text>
                    </Summary>
                    <Summary label={t("devices:detailPage.lastSync")}>
                        <Text data-testid="device-last-sync">
                            {device.lastSyncAt ? new Date(device.lastSyncAt).toLocaleString() : t("devices:schema.never")}
                        </Text>
                    </Summary>
                    <Summary label={t("devices:detailPage.importOnPurchase")}>
                        <Text
                            data-testid="device-import-on-purchase"
                            data-enabled={device.importOnPurchase ? "true" : "false"}
                        >
                            {device.importOnPurchase ? t("common:common.yes") : t("common:common.no")}
                        </Text>
                    </Summary>
                </SimpleGrid>
                <Summary
                    label={usesDefaultTemplate
                        ? t("devices:detailPage.namingTemplateDefault")
                        : t("devices:detailPage.namingTemplate")}
                >
                    <Code
                        block
                        data-testid="device-naming-template"
                        data-default={usesDefaultTemplate ? "true" : "false"}
                    >
                        {namingTemplate ?? ""}
                    </Code>
                </Summary>
            </Stack>

            <Stack gap="xs">
                <Title order={4}>{t("devices:detailPage.sessions")}</Title>
                <DeviceSessionsCollection deviceId={deviceIdNum} autoHeight/>
            </Stack>
        </Stack>
    );
}

function Summary({label, children}: { label: string; children: ReactNode }) {
    return (
        <Stack gap={2}>
            <Text size="sm" c="dimmed">{label}</Text>
            {children}
        </Stack>
    );
}
