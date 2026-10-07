import {useSyncStore} from '../syncStore';

describe('pending prompt', () => {
    beforeEach(() => useSyncStore.getState().reset());

    test('showPrompt keeps the question until it is closed', () => {
        const answer = jest.fn();

        useSyncStore.getState().showPrompt({kind: 'deletion', filePath: 'song.mp3', answer});

        expect(useSyncStore.getState().pendingPrompt).toMatchObject({kind: 'deletion', filePath: 'song.mp3'});

        useSyncStore.getState().closePrompt();

        expect(useSyncStore.getState().pendingPrompt).toBeNull();
        expect(answer).not.toHaveBeenCalled();
    });

    test('cancelling the sync declines an open deletion question for that file only', () => {
        // The sync waits on the answer, so an unanswered question would keep it from stopping
        const answer = jest.fn();
        useSyncStore.getState().showPrompt({kind: 'deletion', filePath: 'song.mp3', answer});

        useSyncStore.getState().cancelSync();

        expect(answer).toHaveBeenCalledWith({value: false, applyToAll: false});
        expect(useSyncStore.getState().pendingPrompt).toBeNull();
    });

    test('resetting the store skips an open conflict question', () => {
        const answer = jest.fn();
        useSyncStore.getState().showPrompt({kind: 'conflict', filePath: 'song.mp3', choices: ['upload', 'skip'], answer});

        useSyncStore.getState().reset();

        expect(answer).toHaveBeenCalledWith({value: 'skip', applyToAll: false});
        expect(useSyncStore.getState().pendingPrompt).toBeNull();
    });
});
