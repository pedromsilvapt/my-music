import {Alert, Button, Center, Group} from "@mantine/core";
import {useDebouncedValue} from "@mantine/hooks";
import {useQueryClient} from "@tanstack/react-query";
import {useCallback, useEffect, useRef, useState} from "react";
import {getListPurchasesQueryKey, useCreatePurchase} from "../../client/purchases.ts";
import {useListSources, type searchSongsResponse, useSearchSongs} from "../../client/sources.ts";
import {API_SEARCH_DEBOUNCE_MS} from "../../consts.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import type {ListSourceItem, SourceSong} from "../../model";
import type {CollectionFilterBarRef} from "../common/collection/collection-filter-bar.tsx";
import Collection from "../common/collection/collection.tsx";
import {IconAlertCircle, IconFilter, IconFilterOff} from "@tabler/icons-react";
import ManageSourcesDialog from "./manage-sources-dialog.tsx";
import SourcesSearchToolbar from "./sources-song-toolbar.tsx";
import {useSourceSongsSchema} from "./useSourceSongsSchema.tsx";
import WishlistModal from "../wishlist/wishlist-modal.tsx";
import {useDisclosure} from "@mantine/hooks";
import {usePurchasesStore} from "../../stores/purchases-store.ts";

export default function SourcesSearch() {
    const queryClient = useQueryClient()
    const searchInputRef = useRef<CollectionFilterBarRef>(null);

    const [search, setSearch] = useState('');
    const [filter, setFilter] = useState('');
    const [appliedFilter, setAppliedFilter] = useState('');
    const [fuzzyMatch, setFuzzyMatch] = useState(true);
    const [source, setSource] = useState<ListSourceItem | null | undefined>(null);
    const [debouncedSearch] = useDebouncedValue(search, API_SEARCH_DEBOUNCE_MS);
    const [manageDialogOpened, setManageDialogOpened] = useState(false);
    const [wishlistOpened, {open: openWishlist, close: closeWishlist}] = useDisclosure(false);

    // Reset fuzzyMatch to true when search or filter changes
    useEffect(() => {
        setFuzzyMatch(true);
    }, [debouncedSearch, appliedFilter]);

    const sourcesQuery = useListSources();
    const sourcesResponse = useQueryData(sourcesQuery, "Failed to fetch sources") ?? {data: {sources: []}};
    const sources = sourcesResponse?.data?.sources ?? [];

    const searchSongsQuery = useSearchSongs(source?.id ?? 0, debouncedSearch, {filter: appliedFilter, fuzzyMatch}, {
        query: {
            placeholderData: (prev) => prev as searchSongsResponse | undefined
        }
    });

    const searchSongsResponse = useQueryData(
        searchSongsQuery,
        "Failed to search songs"
    ) ?? {data: []};

    const {addPendingAutoDownload} = usePurchasesStore();

    const createPurchase = useCreatePurchase({
        mutation: {
            onSuccess: async (response) => {
                queryClient.invalidateQueries({queryKey: getListPurchasesQueryKey()})
                const purchaseId = response.data.purchase.id;
                if (purchaseId != null) {
                    addPendingAutoDownload(purchaseId);
                }
            }
        }
    });

    const onPurchase = useCallback(async (songs: SourceSong[]) => {
        searchInputRef.current?.focusAndSelect();
        await Promise.all(songs.map(s => createPurchase.mutate({
            songId: s.id,
            sourceId: source!.id
        })))
    }, [createPurchase, source]);

    const sourceSongsSchema = useSourceSongsSchema(onPurchase);

    const elements = searchSongsResponse?.data ?? [];

    return <>
        <ManageSourcesDialog
            opened={manageDialogOpened}
            onClose={() => setManageDialogOpened(false)}
        />
        <WishlistModal
            opened={wishlistOpened}
            onClose={closeWishlist}
            currentSource={source}
            currentQuery={search}
            currentFilter={appliedFilter}
            onItemClick={(sourceId, query) => {
                const selectedSource = sources.find(s => s.id === sourceId);
                if (selectedSource) {
                    setSource(selectedSource);
                    setSearch(query);
                }
            }}
        />
        <div style={{height: 'var(--parent-height)'}}>
            {sources.length === 0 ? (
                <div style={{height: '100%', display: 'flex', flexDirection: 'column'}}>
                    <SourcesSearchToolbar
                        onManageSources={() => setManageDialogOpened(true)}
                    />
                    <Center style={{flex: 1}}>
                        <Alert
                            icon={<IconAlertCircle/>}
                            title="No Sources Configured"
                            color="yellow"
                            style={{maxWidth: 400}}
                        >
                            No sources are currently configured. Click the edit button in the toolbar to add sources.
                        </Alert>
                    </Center>
                </div>
            ) : (
                <div style={{height: '100%', display: 'flex', flexDirection: 'column'}}>
                    <div style={{flex: 1, minHeight: 0}}>
                        <Collection
                            key="songs"
                            stateKey="sources-search"
                            items={elements}
                            schema={sourceSongsSchema}
                            isFetching={searchSongsQuery.isFetching}
                            toolbar={p => (
                                <SourcesSearchToolbar
                                    {...p}
                                    searchInputRef={searchInputRef}
                                    source={source}
                                    setSource={setSource}
                                    search={search}
                                    setSearch={setSearch}
                                    filter={filter}
                                    setFilter={setFilter}
                                    onApplyFilter={(filterValue) => {
                                        setFilter(filterValue);
                                        setAppliedFilter(filterValue);
                                    }}
                                    onManageSources={() => setManageDialogOpened(true)}
                                    onOpenWishlist={openWishlist}
                                />
                            )}
                        />
                    </div>
                    <Group justify="center" p="xs">
                        <Button
                            variant={fuzzyMatch ? "light" : "filled"}
                            size="sm"
                            leftSection={fuzzyMatch ? <IconFilter size={16}/> : <IconFilterOff size={16}/>}
                            onClick={() => setFuzzyMatch(!fuzzyMatch)}
                        >
                            {fuzzyMatch ? "Show all results" : "Show matched results"}
                        </Button>
                    </Group>
                </div>
            )}
        </div>
    </>;
}