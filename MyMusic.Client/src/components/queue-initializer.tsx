import {useEffect, useRef} from 'react';
import {useGetCurrentUser} from '../client/users';
import {useQueueManagerStore} from '../stores/queue-manager-store';

export default function QueueInitializer() {
    const {data} = useGetCurrentUser({});
    const setCurrentQueueId = useQueueManagerStore((s) => s.setCurrentQueueId);
    const setVisibleQueueId = useQueueManagerStore((s) => s.setVisibleQueueId);
    const initialized = useRef(false);

    useEffect(() => {
        const currentQueueId = data?.data?.user?.currentQueueId;
        if (currentQueueId && !initialized.current) {
            setCurrentQueueId(currentQueueId);
            setVisibleQueueId(currentQueueId);
            initialized.current = true;
        }
    }, [data, setCurrentQueueId, setVisibleQueueId]);

    return null;
}