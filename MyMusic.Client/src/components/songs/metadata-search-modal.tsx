import {
    Alert,
    Box,
    Center,
    Group,
    Modal,
    ScrollArea,
    Stack,
    Text,
    TextInput,
} from "@mantine/core";
import {useDebouncedValue, useColorScheme} from "@mantine/hooks";
import {IconAlertCircle, IconLoader, IconMusic, IconSearch} from "@tabler/icons-react";
import {useCallback, useEffect, useRef, useState} from "react";
import {useSearchMetadataAllSources} from "../../client/sources.ts";
import {useManualFetchMetadata} from "../../hooks/useManualFetchMetadata";
import type {SongMetadataDiff} from "../../model/songMetadataDiff";
import {API_SEARCH_DEBOUNCE_MS} from "../../consts.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import type {SearchMetadataResult} from "../../model";
import type {GetSongResponseSong} from "../../model/getSongResponseSong";
import {ZINDEX_DRAWER} from "../../consts";
import {ActionIcon, Button} from "@mantine/core";

interface MetadataSearchModalProps {
    opened: boolean;
    onClose: () => void;
    song: GetSongResponseSong;
    onSelect: (metadata: SongMetadataDiff) => void;
}

export default function MetadataSearchModal({
    opened,
    onClose,
    song,
    onSelect,
}: MetadataSearchModalProps) {
    const colorScheme = useColorScheme();
    const [search, setSearch] = useState("");
    const [debouncedSearch] = useDebouncedValue(search, API_SEARCH_DEBOUNCE_MS);
    const scrollAreaRef = useRef<HTMLDivElement>(null);

    useEffect(() => {
        if (opened && song) {
            setSearch(`${song.title} ${song.artists.map(a => a.name).join(" ")}`);
        }
    }, [opened, song]);

    const searchQuery = useSearchMetadataAllSources(debouncedSearch, {
        query: { enabled: opened && debouncedSearch.length > 0 },
    });

    const searchResponse = useQueryData(searchQuery, "Failed to search metadata") ?? { data: { results: [] } };
    const results = searchResponse?.data?.results ?? [];
    const isLoading = searchQuery.isFetching;

    const { manualFetch } = useManualFetchMetadata(onSelect);

    const handleSelect = useCallback(async (result: SearchMetadataResult) => {
        await manualFetch(song.id, result);
        onClose();
    }, [song.id, manualFetch, onClose]);

    return (
        <Modal
            opened={opened}
            onClose={onClose}
            title="Search Metadata"
            size="lg"
            centered
            zIndex={ZINDEX_DRAWER}
        >
            <Stack gap="md">
                <TextInput
                    placeholder="Search for song metadata..."
                    value={search}
                    onChange={(e) => setSearch(e.target.value)}
                    leftSection={<IconSearch size={16}/>}
                    rightSection={isLoading ? <IconLoader className="animate-spin" size={16}/> : null}
                />

                <ScrollArea h={400} viewportRef={scrollAreaRef}>
                    {results.length === 0 ? (
                        <Center h={200}>
                            {debouncedSearch.length > 0 && !isLoading ? (
                                <Alert
                                    icon={<IconAlertCircle/>}
                                    title="No Results"
                                    color="gray"
                                >
                                    No metadata found for "{debouncedSearch}". Try a different search term.
                                </Alert>
                            ) : (
                                <Text c="dimmed">
                                    {search.length === 0
                                        ? "Enter a search term to find metadata from all sources"
                                        : "Searching..."}
                                </Text>
                            )}
                        </Center>
                    ) : (
                        <Stack gap="xs">
                            {results.map((result) => (
                                <MetadataSearchResult
                                    key={`${result.sourceId}-${result.song.id}`}
                                    result={result}
                                    onSelect={handleSelect}
                                    colorScheme={colorScheme}
                                />
                            ))}
                        </Stack>
                    )}
                </ScrollArea>

                <Group justify="flex-end">
                    <Button variant="subtle" onClick={onClose}>
                        Cancel
                    </Button>
                </Group>
            </Stack>
        </Modal>
    );
}

interface MetadataSearchResultProps {
    result: SearchMetadataResult;
    onSelect: (result: SearchMetadataResult) => void;
    colorScheme: "light" | "dark";
}

function MetadataSearchResult({ result, onSelect, colorScheme }: MetadataSearchResultProps) {
    const coverUrl = result.song.cover?.normal ?? result.song.cover?.small ?? null;
    const hoverBg = colorScheme === "dark" ? "var(--mantine-color-dark-5)" : "var(--mantine-color-gray-0)";

    return (
        <Box
            style={{
                padding: "var(--mantine-spacing-sm)",
                borderRadius: "var(--mantine-radius-sm)",
                border: "1px solid var(--mantine-color-gray-3)",
                cursor: "pointer",
                transition: "background-color 0.15s ease",
            }}
            onClick={() => onSelect(result)}
            onMouseEnter={(e) => {
                e.currentTarget.style.backgroundColor = hoverBg;
            }}
            onMouseLeave={(e) => {
                e.currentTarget.style.backgroundColor = "transparent";
            }}
        >
            <Group gap="md" align="flex-start">
                <Box
                    style={{
                        width: 60,
                        height: 60,
                        borderRadius: "var(--mantine-radius-sm)",
                        overflow: "hidden",
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                        backgroundColor: "var(--mantine-color-gray-1)",
                    }}
                >
                    {coverUrl ? (
                        <img
                            src={coverUrl}
                            alt={result.song.title}
                            style={{ width: "100%", height: "100%", objectFit: "cover" }}
                        />
                    ) : (
                        <IconMusic size={24} color="var(--mantine-color-gray-5)" />
                    )}
                </Box>

                <Stack gap={4} style={{ flex: 1 }}>
                    <Text size="sm" fw={500} lineClamp={1}>
                        {result.song.title}
                    </Text>
                    <Text size="xs" c="dimmed" lineClamp={1}>
                        {result.song.artists.map(a => a.name).join(", ")}
                    </Text>
                    <Text size="xs" c="dimmed">
                        {result.song.album?.name ?? "Unknown Album"}
                        {result.song.year ? ` (${result.song.year})` : ""}
                    </Text>
                    <Group gap="xs">
                        <Text size="xs" c="dimmed">
                            Source: {result.sourceName}
                        </Text>
                        {result.song.explicit && (
                            <Text size="xs" c="red">
                                Explicit
                            </Text>
                        )}
                    </Group>
                </Stack>

                <ActionIcon variant="light" size="lg">
                    <IconSearch size={16} />
                </ActionIcon>
            </Group>
        </Box>
    );
}