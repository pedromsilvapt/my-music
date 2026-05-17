import {Modal} from "@mantine/core";
import {useDebouncedValue} from "@mantine/hooks";
import {useCallback, useEffect, useMemo, useState, useRef} from "react";
import {useSearchMetadataAllSources} from "../../client/sources.ts";
import {useManualFetchMetadata} from "../../hooks/useManualFetchMetadata";
import type {SongMetadataDiff} from "../../model/songMetadataDiff";
import {API_SEARCH_DEBOUNCE_MS, ZINDEX_DRAWER} from "../../consts.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import type {SearchMetadataResult} from "../../model";
import type {GetSongResponseSong} from "../../model/getSongResponseSong";
import Collection from "../common/collection/collection";
import CollectionToolbar from "../common/collection/collection-toolbar";
import {CollectionStoreProvider} from "../../contexts/collection-context.tsx";
import {useMetadataSearchSchema, type MetadataSearchItem} from "./use-metadata-search-schema";

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

    const searchResponse = useQueryData(searchQuery, "Failed to search metadata") ?? {data: {results: []}};
    const results = searchResponse?.data?.results ?? [];
    const isFetching = searchQuery.isFetching;

    const {manualFetch} = useManualFetchMetadata(onSelect);
    const [isApplying, setIsApplying] = useState(false);
    const isApplyingRef = useRef(false);

    const handleApply = useCallback(async (item: MetadataSearchItem) => {
        if (isApplyingRef.current) return;
        isApplyingRef.current = true;
        setIsApplying(true);
        try {
            await manualFetch(song.id, item);
            onClose();
        } finally {
            isApplyingRef.current = false;
            setIsApplying(false);
        }
    }, [song.id, manualFetch, onClose]);

    const schema = useMetadataSearchSchema(handleApply);

    const items = useMemo(() =>
        results.map((result: SearchMetadataResult): MetadataSearchItem => ({
            ...result,
            id: `${result.sourceId}-${result.song.id}`,
        })),
        [results],
    );

    const combinedFetching = isFetching || isApplying;

    return (
        <Modal
            opened={opened}
            onClose={onClose}
            title="Search Metadata"
            size="lg"
            centered
            zIndex={ZINDEX_DRAWER}
        >
            <div data-testid="metadata-search" data-loading={combinedFetching ? "true" : "false"} style={{height: 400}}>
            <CollectionStoreProvider>
                <Collection
                    items={items}
                    schema={schema}
                    initialView="list"
                    stateKey="metadata-search"
                    isFetching={combinedFetching}
                    selectable={false}
                    filterMode="server"
                    serverSearch={debouncedSearch}
                    onServerFilterChange={(searchValue) => setSearch(searchValue)}
                    searchPlaceholder="Search for song metadata..."
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
        </Modal>
    );
}