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
import {getSong, useSearchMetadataAllSources} from "../../client/sources.ts";
import {API_SEARCH_DEBOUNCE_MS} from "../../consts.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import type {SearchMetadataResult, SongMetadataDiff, SourceSong} from "../../model";
import type {GetSongResponseSong} from "../../model/getSongResponseSong";
import {ZINDEX_DRAWER} from "../../consts";
import {ActionIcon, Button} from "@mantine/core";

interface MetadataSearchModalProps {
    opened: boolean;
    onClose: () => void;
    song: GetSongResponseSong;
    onSelect: (metadata: SongMetadataDiff) => void;
}

function createMetadataDiff(song: GetSongResponseSong, sourceSong: SourceSong): SongMetadataDiff {
    const diff: SongMetadataDiff = {};

    if (sourceSong.title?.trim() && !stringEquals(song.title, sourceSong.title)) {
        diff.title = { old: song.title, new: sourceSong.title };
    }

    if (sourceSong.year != null && sourceSong.year > 0 && song.year !== sourceSong.year) {
        diff.year = { old: song.year ?? 0, new: sourceSong.year };
    }

    if (sourceSong.lyrics?.trim() && !stringEquals(song.lyrics ?? "", sourceSong.lyrics)) {
        diff.lyrics = { old: song.lyrics ?? "", new: sourceSong.lyrics };
    }

    if (sourceSong.explicit != null && sourceSong.explicit !== song.isExplicit) {
        diff.explicit = { old: song.isExplicit, new: sourceSong.explicit };
    }

    if (sourceSong.album?.name?.trim() && !stringEquals(song.album?.name, sourceSong.album.name)) {
        diff.album = {
            old: { name: song.album?.name ?? "" },
            new: { name: sourceSong.album.name },
        };
    }

    if (sourceSong.album?.artist?.name?.trim() && !stringEquals(song.album?.artist?.name, sourceSong.album.artist.name)) {
        (diff as any).albumArtist = {
            old: song.album?.artist?.name ?? "",
            new: sourceSong.album.artist.name,
        };
    }

    const songArtistNames = song.artists.map(a => a.name);
    const sourceArtistNames = sourceSong.artists.map(a => a.name);

    if (sourceArtistNames.length > 0 && !arraysEqual(songArtistNames, sourceArtistNames)) {
        diff.artists = {
            old: songArtistNames.map(name => ({ name })),
            new: sourceArtistNames.map(name => ({ name })),
        };
    }

    const songGenreNames = song.genres.map(g => g.name);
    const sourceGenreNames = sourceSong.genres;

    if (sourceGenreNames.length > 0 && !arraysEqual(songGenreNames, sourceGenreNames)) {
        diff.genres = {
            old: songGenreNames,
            new: sourceGenreNames,
        };
    }

    if (sourceSong.cover) {
        const oldCoverUrl = song.cover ? `/api/artwork/${song.cover}` : "";
        diff.cover = { old: oldCoverUrl, new: sourceSong.cover.biggest ?? "" };
    }

    return diff;
}

function stringEquals(a: string | null | undefined, b: string | null | undefined): boolean {
    return (a ?? "").toLowerCase() === (b ?? "").toLowerCase();
}

function arraysEqual(a: string[], b: string[]): boolean {
    if (a.length !== b.length) return false;
    return a.every((val, idx) => stringEquals(val, b[idx]));
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

    const handleSelect = useCallback(async (result: SearchMetadataResult) => {
        try {
            const response = await getSong(result.sourceId, result.song.id);
            const fullDetails = response.data;
            const diff = createMetadataDiff(song, fullDetails);
            onSelect(diff);
        } catch {
            const diff = createMetadataDiff(song, result.song);
            onSelect(diff);
        }
        onClose();
    }, [song, onSelect, onClose]);

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