import {Badge, Box, Button, Collapse, Group, Modal, ScrollArea, SegmentedControl, Stack, Text, TextInput} from "@mantine/core";
import {notifications} from "@mantine/notifications";
import {useState} from "react";
import {useTranslation} from "react-i18next";
import {IconChevronDown, IconChevronUp, IconShare} from "@tabler/icons-react";
import {useListSongSharesBatch} from "../../client/song-sharing.ts";
import {useListSongs} from "../../client/songs.ts";
import {useManageSongSharesWithSongsInvalidation} from "../../hooks/use-manage-song-shares.ts";
import {useGetCurrentUser, useListUsers} from "../../client/users.ts";
import {ZINDEX_MODAL} from "../../consts.ts";
import {useQueryData} from "../../hooks/use-query-data.ts";
import type {ListSongItem, ListUserItem} from "../../model";
import ManageSongItem from "../common/manage-song-item.tsx";

type ShareSelection = "none" | "add" | "remove";

interface ManageSharingDialogProps {
    opened: boolean;
    onClose: () => void;
    songIds: number[];
    onSuccess?: () => void;
}

export default function ManageSharingDialog({
                                                opened,
                                                onClose,
                                                songIds,
                                                onSuccess
                                               }: ManageSharingDialogProps) {
    const {t} = useTranslation(["sharing", "common"]);
    const songsQuery = useListSongs(
        songIds.length > 0 ? {filter: `id in [${songIds.join(',')}]`} : undefined,
        {query: {enabled: opened && songIds.length > 0}}
    );
    const songsResponse = useQueryData(songsQuery, t("sharing:manageDialog.fetchSongsFailed")) ?? {data: {songs: []}};
    const songs = songsResponse?.data?.songs ?? [];
    const ownedSongs = songs.filter(s => !s.isShared);

    const usersQuery = useListUsers({query: {enabled: opened, refetchOnMount: 'always'}});
    const usersResponse = useQueryData(usersQuery, t("sharing:manageDialog.fetchUsersFailed")) ?? {data: {users: []}};
    const users = usersResponse?.data?.users ?? [];

    const currentUserQuery = useGetCurrentUser({query: {enabled: opened}});
    const currentUserResponse = useQueryData(currentUserQuery, t("sharing:manageDialog.fetchCurrentUserFailed"));
    const currentUserId = currentUserResponse?.data?.user?.id;

    const recipients = users.filter(u => u.id !== currentUserId);

    const sharesQuery = useListSongSharesBatch(
        ownedSongs.length > 0 ? {songIds: ownedSongs.map(s => s.id).join(',')} : undefined,
        {query: {enabled: opened && ownedSongs.length > 0}}
    );
    const sharesResponse = useQueryData(sharesQuery, t("sharing:manageDialog.fetchSharesFailed")) ?? {data: {shares: []}};
    const existingShares = sharesResponse?.data?.shares ?? [];

    const [selections, setSelections] = useState<Map<number, ShareSelection>>(new Map());
    const [userSearch, setUserSearch] = useState("");
    const [expandedUsers, setExpandedUsers] = useState<Set<number>>(new Set());

    const manageShares = useManageSongSharesWithSongsInvalidation({
        mutation: {
            onSuccess: () => {
                setSelections(new Map());
                setUserSearch("");
                setExpandedUsers(new Set());
                onClose();
                onSuccess?.();
            },
            onError: (error: unknown) => {
                const errorResponse = error as { response?: { data?: { detail?: string } }; message?: string } | null;
                const errorMessage = errorResponse?.response?.data?.detail
                    ?? errorResponse?.message
                    ?? t("sharing:manageDialog.updateFailedFallback");
                notifications.show({
                    title: t("common:status.error"),
                    message: errorMessage,
                    color: 'red',
                });
                console.error('Failed to update sharing:', error);
            }
        }
    });

    const handleSelectionChange = (userId: number, value: string) => {
        setSelections(prev => {
            const newMap = new Map(prev);
            const valueEnum = value as ShareSelection;
            if (valueEnum === "none") {
                newMap.delete(userId);
            } else {
                newMap.set(userId, valueEnum);
            }
            return newMap;
        });
    };

    const handleApply = () => {
        const shares: { userId: number; action: string }[] = [];

        selections.forEach((selection, userId) => {
            if (selection === "add" || selection === "remove") {
                shares.push({userId, action: selection});
            }
        });

        if (shares.length > 0) {
            manageShares.mutate({
                data: {
                    songIds: ownedSongs.map(s => s.id),
                    shares,
                }
            });
        } else {
            onClose();
        }
    };

    const handleCancel = () => {
        setSelections(new Map());
        setUserSearch("");
        setExpandedUsers(new Set());
        onClose();
    };

    const handleToggleExpand = (userId: number) => {
        setExpandedUsers(prev => {
            const next = new Set(prev);
            if (next.has(userId)) {
                next.delete(userId);
            } else {
                next.add(userId);
            }
            return next;
        });
    };

    const filteredRecipients = userSearch.trim() === ""
        ? recipients
        : recipients.filter(u =>
            u.username.toLowerCase().includes(userSearch.toLowerCase())
            || u.name.toLowerCase().includes(userSearch.toLowerCase())
        );

    const sharesByUserSongId = new Map<number, Set<number>>();
    const ownedSongIdSet = new Set(ownedSongs.map(s => s.id));
    for (const share of existingShares) {
        if (ownedSongIdSet.has(share.songId)) {
            let set = sharesByUserSongId.get(share.userId);
            if (!set) {
                set = new Set();
                sharesByUserSongId.set(share.userId, set);
            }
            set.add(share.songId);
        }
    }

    return (
        <Modal opened={opened} onClose={handleCancel} size="lg" title={t("sharing:manageDialog.title")} centered
               zIndex={ZINDEX_MODAL}>
            <Stack data-testid="manage-sharing"
                   data-loading={sharesQuery.isFetching ? "true" : "false"}>
                <Group justify="space-between" align="center">
                    <Text size="sm" c="dimmed">
                        {ownedSongs.length === 0
                            ? t("sharing:manageDialog.noOwnedSongs")
                            : t("sharing:manageDialog.managing", {count: ownedSongs.length})}
                    </Text>
                    <TextInput
                        placeholder={t("sharing:manageDialog.filterPlaceholder")}
                        value={userSearch}
                        onChange={(e) => setUserSearch(e.target.value)}
                        size="xs"
                        w={240}
                    />
                </Group>

                {ownedSongs.length === 0 ? (
                    <Text c="dimmed" ta="center" py="xl">{t("sharing:manageDialog.noOwnedSongs")}</Text>
                ) : (
                    <ScrollArea h={400}>
                        <Stack gap="sm">
                            {filteredRecipients.map(user => (
                                <ShareRow
                                    key={user.id}
                                    user={user}
                                    sharedSongIds={sharesByUserSongId.get(user.id) ?? new Set()}
                                    ownedSongs={ownedSongs}
                                    expanded={expandedUsers.has(user.id)}
                                    onToggleExpand={() => handleToggleExpand(user.id)}
                                    value={selections.get(user.id) ?? "none"}
                                    onChange={(value) => handleSelectionChange(user.id, value)}
                                />
                            ))}
                            {filteredRecipients.length === 0 && (
                                <Text c="dimmed" ta="center" py="sm">{t("sharing:manageDialog.noRecipients")}</Text>
                            )}
                        </Stack>
                    </ScrollArea>
                )}

                <Group justify="flex-end">
                    <Button variant="default" onClick={handleCancel}>
                        {t("common:actions.cancel")}
                    </Button>
                    <Button
                        onClick={handleApply}
                        loading={manageShares.isPending}
                        disabled={ownedSongs.length === 0}
                        leftSection={<IconShare size={16}/>}
                    >
                        {t("common:actions.apply")}
                    </Button>
                </Group>
            </Stack>
        </Modal>
    );
}

interface ShareRowProps {
    user: ListUserItem;
    sharedSongIds: Set<number>;
    ownedSongs: ListSongItem[];
    expanded: boolean;
    onToggleExpand: () => void;
    value: ShareSelection;
    onChange: (value: ShareSelection) => void;
}

function ShareRow({user, sharedSongIds, ownedSongs, expanded, onToggleExpand, value, onChange}: ShareRowProps) {
    const {t} = useTranslation(["sharing", "common"]);
    const matchCount = ownedSongs.filter(s => sharedSongIds.has(s.id)).length;

    return (
        <Box data-testid="share-row" data-share-user-id={user.id} data-share-username={user.username}>
            <Group justify="space-between" wrap="nowrap">
                <Group gap="xs" align="center" style={{flex: 1, minWidth: 0}}>
                    <Stack gap={0} style={{flex: 1, minWidth: 0}}>
                        <Text fw={500} truncate>{user.name}</Text>
                        <Text size="xs" c="dimmed" truncate>{user.username}</Text>
                    </Stack>
                </Group>
                <Group gap="xs" wrap="nowrap">
                    <Badge
                        data-testid="share-expand-badge"
                        size="sm"
                        variant="light"
                        color={matchCount > 0 ? "green" : "gray"}
                        onClick={onToggleExpand}
                        style={{cursor: 'pointer'}}
                        leftSection={
                            expanded ? <IconChevronUp size={12}/> : <IconChevronDown size={12}/>
                        }
                    >
                        {matchCount}/{ownedSongs.length}
                    </Badge>
                    <SegmentedControl
                        value={value}
                        onChange={(v) => onChange(v as ShareSelection)}
                        data={[
                            {label: <Text inherit c="gray">{t("common:common.none")}</Text>, value: 'none'},
                            {label: <Text inherit c={value === 'add' ? 'green' : 'gray'}>{t("common:common.add")}</Text>, value: 'add'},
                            {label: <Text inherit c={value === 'remove' ? 'red' : 'gray'}>{t("common:common.remove")}</Text>, value: 'remove'},
                        ]}
                        size="xs"
                    />
                </Group>
            </Group>
            <Collapse in={expanded}>
                <Stack gap="xs" pl="sm" pt="xs">
                    {ownedSongs.map(song => {
                        const isShared = sharedSongIds.has(song.id);
                        return (
                            <ManageSongItem
                                key={song.id}
                                song={song}
                                isIncluded={isShared}
                            />
                        );
                    })}
                </Stack>
            </Collapse>
        </Box>
    );
}