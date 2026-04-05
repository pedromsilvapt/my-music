import {ActionIcon, Center, Group, SegmentedControl} from "@mantine/core";
import {useUncontrolled} from "@mantine/hooks";
import {useEffect, useMemo} from "react";
import {useListSources} from "../../client/sources.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import type {ListSourceItem, SourceSong} from "../../model";
import CollectionToolbar, {type CollectionToolbarProps} from "../common/collection/collection-toolbar.tsx";
import {CollectionFilterBar, type CollectionFilterBarRef} from "../common/collection/collection-filter-bar.tsx";
import TablerIcon from "../common/tabler-icon.tsx";
import {IconHeart, IconPencil} from "@tabler/icons-react";

export interface SourcesSearchToolbarProps extends CollectionToolbarProps<SourceSong> {
    source?: ListSourceItem | null | undefined,
    setSource?: (source: ListSourceItem | null | undefined) => void,
    searchInputRef?: React.RefObject<CollectionFilterBarRef | null>;
    onManageSources?: () => void;
    onOpenWishlist?: () => void;
}

export default function SourcesSearchToolbar(props: SourcesSearchToolbarProps) {
    const [source, setSource] = useUncontrolled<ListSourceItem | null | undefined>({
        value: props.source,
        onChange: props.setSource,
    });

    const [search, setSearch] = useUncontrolled({
        value: props.search,
        defaultValue: '',
        onChange: props.setSearch,
    });

    const [filter, setFilter] = useUncontrolled({
        value: props.filter,
        defaultValue: '',
        onChange: props.setFilter,
    });

    const sourcesQuery = useListSources();

    const sourcesResponse = useQueryData(sourcesQuery, "Failed to fetch sources") ?? {data: {sources: []}};

    const sources = sourcesResponse.data.sources;

    const sourcesOptions = useMemo(() => sources.map(source => ({
        value: '' + source.id,
        label: (
            <Center style={{gap: 10}}>
                <TablerIcon icon={source.icon} size={16}/>
                <span>{source.name}</span>
            </Center>
        )
    })), [sources]);

    useEffect(() => {
        if (source == null && sources.length > 0) {
            setSource(sources[0]);
        }
    }, [source, sources, setSource]);

    return (
        <CollectionToolbar
            {...props}
            search={search}
            setSearch={setSearch}
            filter={filter}
            setFilter={setFilter}
            searchInputRef={props.searchInputRef}
            filterMode="server"
            searchPlaceholder="Search songs..."
            renderLeftSection={() => (
                <Group gap="xs" wrap="nowrap">
                    <SegmentedControl
                        value={source?.id?.toString()}
                        onChange={sourceId => setSource(sources.find(s => s.id.toString() === sourceId))}
                        data={sourcesOptions}
                    />
                    <ActionIcon
                        variant="subtle"
                        size="lg"
                        onClick={props.onManageSources}
                        title="Manage sources"
                    >
                        <IconPencil size={18}/>
                    </ActionIcon>
                </Group>
            )}
            renderMiddleSection={() => (
                <Group gap="sm" align="center" justify="space-between" style={{flex: 1}}>
                    <CollectionFilterBar
                        ref={props.searchInputRef}
                        searchValue={search}
                        onSearchChange={setSearch}
                        filterValue={filter}
                        onFilterChange={setFilter}
                        onApply={(filterValue) => {
                            setFilter(filterValue);
                            props.onApplyFilter?.(filterValue);
                        }}
                        filterMode="server"
                        placeholder="Search songs..."
                        filterMetadata={props.filterMetadata}
                        fetchFilterValues={props.fetchFilterValues}
                    />
                    <ActionIcon
                        variant="subtle"
                        size="lg"
                        onClick={props.onOpenWishlist}
                        title="Wishlist"
                    >
                        <IconHeart size={18}/>
                    </ActionIcon>
                </Group>
            )}
        />
    );
}