import {Button, Group, Menu, Modal, Stack} from "@mantine/core";
import {IconChevronDown} from "@tabler/icons-react";
import {useCallback, useState} from "react";
import {useTranslation} from "react-i18next";
import {useGetDevices} from "../../client/devices.ts";
import {ZINDEX_DRAWER, ZINDEX_LIGHTBOX} from "../../consts.ts";
import type {PreviewDeviceNamingTemplateResponse} from "../../model";
import TablerIcon from "../common/tabler-icon.tsx";
import {DEFAULT_DEVICE_ICON} from "../devices/device-icons.ts";
import NamingTemplateField from "../devices/naming-template-field.tsx";

interface SongNamingTemplateModalProps {
    opened: boolean;
    onClose: () => void;
    songId: number;
}

/**
 * A sandbox to try naming templates on a song: shows the path a template would give to the song when added to
 * a device. Nothing is saved.
 */
export default function SongNamingTemplateModal({opened, onClose, songId}: SongNamingTemplateModalProps) {
    const {t} = useTranslation(["songs", "common"]);

    return (
        <Modal
            opened={opened}
            onClose={onClose}
            title={t("songs:tools.testNamingTemplate.title")}
            centered
            size="lg"
            zIndex={ZINDEX_DRAWER}
        >
            {/* Only rendered while opened, so the template starts over each time */}
            {opened && <SongNamingTemplateForm songId={songId} onClose={onClose}/>}
        </Modal>
    );
}

interface SongNamingTemplateFormProps {
    songId: number;
    onClose: () => void;
}

function SongNamingTemplateForm({songId, onClose}: SongNamingTemplateFormProps) {
    const {t} = useTranslation(["songs", "common"]);
    const [template, setTemplate] = useState("");
    // The template of the devices that have none of their own
    const [defaultTemplate, setDefaultTemplate] = useState("");

    const devicesQuery = useGetDevices();
    const devices = devicesQuery.data?.data.devices ?? [];

    const handlePreviewChange = useCallback(
        (preview: PreviewDeviceNamingTemplateResponse) => setDefaultTemplate(preview.defaultNamingTemplate),
        []
    );

    return (
        <Stack gap="md" data-testid="song-naming-template-modal">
            <Group>
                <Menu shadow="md" position="bottom-start" withinPortal zIndex={ZINDEX_LIGHTBOX}>
                    <Menu.Target>
                        <Button
                            variant="light"
                            rightSection={<IconChevronDown size={16}/>}
                            loading={devicesQuery.isLoading}
                            data-testid="song-naming-template-starters"
                        >
                            {t("songs:tools.testNamingTemplate.starters")}
                        </Button>
                    </Menu.Target>
                    <Menu.Dropdown mah={300} style={{overflowY: "auto"}}>
                        {devices.length === 0 && (
                            <Menu.Item disabled>{t("songs:tools.testNamingTemplate.noDevices")}</Menu.Item>
                        )}
                        {devices.map(device => (
                            <Menu.Item
                                key={device.id}
                                leftSection={
                                    <TablerIcon
                                        icon={device.icon}
                                        defaultIcon={DEFAULT_DEVICE_ICON}
                                        size={16}
                                        color={device.color || "gray"}
                                    />
                                }
                                onClick={() => setTemplate(device.namingTemplate ?? defaultTemplate)}
                                data-testid="song-naming-template-starter"
                                data-device-id={device.id}
                            >
                                {device.name}
                            </Menu.Item>
                        ))}
                    </Menu.Dropdown>
                </Menu>
            </Group>
            <NamingTemplateField
                songId={songId}
                value={template}
                onChange={setTemplate}
                onPreviewChange={handlePreviewChange}
                description={t("songs:tools.testNamingTemplate.description")}
            />
            <Group justify="flex-end">
                <Button variant="subtle" onClick={onClose}>
                    {t("common:actions.close")}
                </Button>
            </Group>
        </Stack>
    );
}
