import {Modal, Stack} from "@mantine/core";
import {useDebouncedValue} from "@mantine/hooks";
import {useCallback, useEffect, useMemo, useState, useRef} from "react";
import {useTranslation} from "react-i18next";
import {useSearchMetadataAllSources} from "../../client/sources.ts";
import {API_SEARCH_DEBOUNCE_MS, ZINDEX_DRAWER} from "../../consts.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import type {SearchMetadataResult} from "../../model";
import type {GetSongResponseSong} from "../../model/getSongResponseSong";
import Collection from "../common/collection/collection";
import CollectionToolbar from "../common/collection/collection-toolbar";
import {CollectionStoreProvider} from "../../contexts/collection-context.tsx";
import {
    useMetadataSearchSchema,
    type MetadataSearchAction,
    type MetadataSearchItem,
} from "./use-metadata-search-schema";

interface SourceSongSearchModalProps {
    opened: boolean;
    onClose: () => void;
    /** The song the search is for: its title and artists are the initial search. */
    song: GetSongResponseSong;
    title: string;
    searchPlaceholder: string;
    /** Identifies the results in tests, and the key their view state is stored under. */
    testId: string;
    action: MetadataSearchAction;
    /**
     * Called with the result the user picked. The results show as loading until it settles, and the modal closes
     * when it resolves to anything but false.
     */
    onPick: (item: MetadataSearchItem) => Promise<boolean | void>;
    /** Shown above the results. */
    header?: React.ReactNode;
}

/**
 * Searches all the sources for the songs matching a song of the library, and lets the user pick one of them.
 */
export default function SourceSongSearchModal({
    opened,
    onClose,
    song,
    title,
    searchPlaceholder,
    testId,
    action,
    onPick,
    header,
}: SourceSongSearchModalProps) {
    const {t} = useTranslation(["songs", "common"]);
    const [search, setSearch] = useState("");
    const [debouncedSearch] = useDebouncedValue(search, API_SEARCH_DEBOUNCE_MS);

    useEffect(() => {
        if (opened && song) {
            setSearch(`${song.title} ${song.artists.map(a => a.name).join(" ")}`);
        }
    }, [opened, song]);

    const searchQuery = useSearchMetadataAllSources(debouncedSearch, {
        query: {enabled: opened && debouncedSearch.length > 0},
    });

    const searchResponse = useQueryData(searchQuery, t("songs:metadataSearch.searchFailed")) ?? {data: {results: []}};
    const results = searchResponse?.data?.results ?? [];
    const isFetching = searchQuery.isFetching;

    const [isPicking, setIsPicking] = useState(false);
    const isPickingRef = useRef(false);

    const handlePick = useCallback(async (item: MetadataSearchItem) => {
        if (isPickingRef.current) return;
        isPickingRef.current = true;
        setIsPicking(true);
        try {
            if (await onPick(item) !== false) {
                onClose();
            }
        } finally {
            isPickingRef.current = false;
            setIsPicking(false);
        }
    }, [onPick, onClose]);

    const schema = useMetadataSearchSchema(handlePick, action);

    const items = useMemo(() =>
        results.map((result: SearchMetadataResult): MetadataSearchItem => ({
            ...result,
            id: `${result.sourceId}-${result.song.id}`,
        })),
        [results],
    );

    const combinedFetching = isFetching || isPicking;

    return (
        <Modal
            opened={opened}
            onClose={onClose}
            title={title}
            size="lg"
            centered
            zIndex={ZINDEX_DRAWER}
        >
            <Stack gap="md">
                {header}
                <div data-testid={testId} data-loading={combinedFetching ? "true" : "false"} style={{height: 400}}>
                    <CollectionStoreProvider>
                        <Collection
                            items={items}
                            schema={schema}
                            initialView="list"
                            stateKey={testId}
                            isFetching={combinedFetching}
                            selectable={false}
                            filterMode="server"
                            serverSearch={debouncedSearch}
                            onServerFilterChange={(searchValue) => setSearch(searchValue)}
                            searchPlaceholder={searchPlaceholder}
                            toolbar={(p) => (
                                <CollectionToolbar
                                    {...p}
                                    renderLeftSection={() => null}
                                    renderRightSection={() => null}
                                />
                            )}
                        />
                    </CollectionStoreProvider>
                </div>
            </Stack>
        </Modal>
    );
}
