import {useTranslation} from "react-i18next";
import {Badge, Group, Tooltip} from "@mantine/core";
import {IconCheck, IconPointFilled} from "@tabler/icons-react";
import type {SyncRecordAction, SyncSessionItem} from "../../model";
import {areSkippedRecordsDeleted, buildActionFilter, getActionColor, getActionCount, isActionFilterable, parseActionFilter, SYNC_RECORD_ACTIONS} from "./sync-record-action.ts";

interface SessionActionPillsProps {
    session?: SyncSessionItem;
    filter: string;
    onFilterChange: (filter: string) => void;
}

/**
 * One pill per sync record action, with the session's record count. Clicking a pill toggles that
 * action in the advanced filter expression; pills with no records are disabled, except Conflict,
 * whose count leaves out the resolved conflicts that are still listed. Skipped files are counted
 * even when their records were not kept, in which case the pill explains why it is disabled.
 */
export default function SessionActionPills({session, filter, onFilterChange}: SessionActionPillsProps) {
    const {t} = useTranslation(["devices"]);
    // A custom (non pill-generated) filter shows no selection; clicking a pill replaces it
    const selected = parseActionFilter(filter) ?? [];

    const toggle = (action: SyncRecordAction) => {
        const next = selected.includes(action)
            ? selected.filter(a => a !== action)
            : [...selected, action];
        onFilterChange(buildActionFilter(next));
    };

    return (
        <Group gap="xs" mb="sm" justify="center" data-testid="session-action-pills" role="group" aria-label={t("devices:recordsPage.actionPillsLabel")}>
            {SYNC_RECORD_ACTIONS.map(action => {
                const count = session ? getActionCount(session, action) : 0;
                const isSelected = selected.includes(action);
                const disabled = !session || !isActionFilterable(session, action);
                const pill = (
                    <Badge
                        key={action}
                        component="button"
                        type="button"
                        color={getActionColor(action)}
                        variant={isSelected ? "filled" : "light"}
                        disabled={disabled}
                        aria-pressed={isSelected}
                        leftSection={isSelected ? <IconCheck size={12} stroke={3}/> : <IconPointFilled size={12}/>}
                        onClick={() => toggle(action)}
                        style={{
                            cursor: disabled ? 'not-allowed' : 'pointer',
                            opacity: disabled ? 0.4 : 1,
                            border: 'none',
                        }}
                    >
                        {action} {count}
                    </Badge>
                );

                return session && areSkippedRecordsDeleted(session, action)
                    // A disabled button fires no pointer events, so the tooltip is anchored to a wrapper
                    ? (
                        <Tooltip key={action} label={t("devices:recordsPage.skippedNotRecorded")}>
                            <span style={{display: 'inline-flex'}}>{pill}</span>
                        </Tooltip>
                    )
                    : pill;
            })}
        </Group>
    );
}
