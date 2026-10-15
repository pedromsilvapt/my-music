import {useCallback, useState} from 'react';
import {Alert} from 'react-native';
import {runRepairDates} from '../services/syncService';
import {buildRepairDatesSummary, type RepairDatesResult} from '../services/sync/repair-dates';

export type RepairDatesStatus =
    | {state: 'idle'}
    | {state: 'previewing' | 'repairing'; done: number; total: number};

function confirm(title: string, message: string, confirmText: string): Promise<boolean> {
    return new Promise((resolve) => {
        Alert.alert(title, message, [
            {text: 'Cancel', style: 'cancel', onPress: () => resolve(false)},
            {text: confirmText, onPress: () => resolve(true)},
        ], {cancelable: true, onDismiss: () => resolve(false)});
    });
}

function describeResult(result: RepairDatesResult): string {
    const summary = buildRepairDatesSummary(result);
    return result.failed > 0 ? `${summary}\n\n${result.failed} could not be re-dated.` : summary;
}

/**
 * Repairs the dates of the synced files: counts what would change with a dry run, asks to go on, then re-dates
 * the files, reporting how many are done.
 */
export function useRepairDates() {
    const [status, setStatus] = useState<RepairDatesStatus>({state: 'idle'});

    const start = useCallback(async () => {
        try {
            setStatus({state: 'previewing', done: 0, total: 0});
            const preview = await runRepairDates(true);

            if (preview.redated === 0) {
                setStatus({state: 'idle'});
                Alert.alert('Nothing to Re-date', buildRepairDatesSummary(preview));
                return;
            }

            setStatus({state: 'idle'});
            const proceed = await confirm(
                'Repair Dates',
                `${buildRepairDatesSummary(preview)}\n\nThe files will get the date set in "Date of Downloaded Files".`,
                `Re-date ${preview.redated} ${preview.redated === 1 ? 'file' : 'files'}`
            );
            if (!proceed) {
                return;
            }

            setStatus({state: 'repairing', done: 0, total: 0});
            const result = await runRepairDates(false, (done, total) => setStatus({state: 'repairing', done, total}));
            setStatus({state: 'idle'});
            Alert.alert('Dates Repaired', describeResult(result));
        } catch (error: any) {
            setStatus({state: 'idle'});
            console.error('Failed to repair the dates of the synced files:', error);
            Alert.alert('Repair Failed', error?.message || 'Failed to repair the dates of the synced files');
        }
    }, []);

    return {status, start};
}
