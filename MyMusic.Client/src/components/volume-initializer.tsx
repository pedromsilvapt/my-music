import {useEffect, useRef} from 'react';
import {usePlaybackActions} from '../stores/playback-store';
import {useUserPreferences} from '../hooks/use-user-preferences';

export default function VolumeInitializer() {
    const {user, volume, isMuted} = useUserPreferences();
    const {initializeFromUser} = usePlaybackActions((s) => ({initializeFromUser: s.initializeFromUser}));
    const initialized = useRef(false);

    useEffect(() => {
        if (user.id > 0 && !initialized.current) {
            initializeFromUser(volume, isMuted);
            initialized.current = true;
        }
    }, [user.id, volume, isMuted, initializeFromUser]);

    return null;
}
