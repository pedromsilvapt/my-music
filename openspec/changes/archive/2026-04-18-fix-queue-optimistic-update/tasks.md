## 1. Helper Function

- [x] 1.1 Add `filterOutExistingSongs()` private function in `use-queue.ts` that takes a queue and a Set of songIds, returns the queue with those songs removed and orders re-compacted

## 2. Update playNext Optimistic Update

- [x] 2.1 Update `playNext()` function to filter out existing songs from the queue before calling `insertAfterCurrent()`
- [x] 2.2 Verify the optimistic queue correctly reflects moved songs (no duplicates)

## 3. Update playLast Optimistic Update

- [x] 3.1 Update `playLast()` function to filter out existing songs from the queue before calling `appendToEnd()`
- [x] 3.2 Verify the optimistic queue correctly reflects moved songs (no duplicates)

## 4. Testing

- [x] 4.1 Add unit test for `playNext()` with songs already in queue (verifies songs are moved, not duplicated)
- [x] 4.2 Add unit test for `playNext()` with mix of new and existing songs
- [x] 4.3 Add unit test for `playLast()` with songs already in queue (verifies songs are moved, not duplicated)
- [x] 4.4 Add unit test for `playLast()` with mix of new and existing songs
- [x] 4.5 Add unit test for `filterOutExistingSongs()` helper function

## 5. Verification

- [x] 5.1 Run `npm run lint` in MyMusic.Client
- [x] 5.2 Run `npm run build` in MyMusic.Client
- [ ] 5.3 Manual test: Select multiple songs in Now Playing list, use "Play Next", verify no duplicates appear
- [ ] 5.4 Manual test: Select multiple songs in Now Playing list, use "Play Last", verify no duplicates appear
