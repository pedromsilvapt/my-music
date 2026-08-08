import {Box, Container, Select, Stack, Text} from "@mantine/core";
import {useTranslation} from "react-i18next";
import {LANGUAGE_OPTIONS, isSupportedLanguage} from "../../locales";
import {useUserPreferences} from "../../hooks/use-user-preferences";

export default function SettingsPage() {
    const {t} = useTranslation(["settings", "common"]);
    const {user, updateLanguage, isLoading, isUpdating} = useUserPreferences();

    const currentLanguage = isSupportedLanguage(user.language) ? user.language : "en";

    return (
        <Container data-testid="settings" data-loading={isLoading ? "true" : "false"}>
            <Stack gap="md" mt="md">
                <Box>
                    <Text fw={600} size="lg" mb="xs">{t("settings:language")}</Text>
                    <Text size="sm" c="dimmed" mb="sm">{t("settings:languageHelp")}</Text>
                    <Select
                        data-testid="settings-language"
                        hiddenInputProps={{ "data-testid": "settings-language-value" }}
                        label={t("settings:language")}
                        value={currentLanguage}
                        data={LANGUAGE_OPTIONS.map((o) => ({value: o.value, label: o.label}))}
                        onChange={(v) => {
                            if (v) void updateLanguage(v);
                        }}
                        disabled={isUpdating}
                        w={260}
                    />
                </Box>
            </Stack>
        </Container>
    );
}