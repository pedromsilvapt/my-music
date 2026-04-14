---
description: Fix open review comments
---

Open review threads:

!`./.localea/reviews/list.sh open`

If the last reply in a thread is from the assistant, and the user has not reacted to it or replied, skip that thread until new information arrives.

User specific instructions:
---
$ARGUMENTS
---

If the user gave specific instructions when executing this command, assess how those instructions relate to the comments above, and adjust accordingly.

Group related comments by thread. For each group, sequentially spawn a sub-agent task to either:
1. Propose a solution and reply via `./.localea/reviews/reply.sh <thread-id> open '<response>'`
2. If user already approved, implement changes then reply via `./.localea/reviews/reply.sh <thread-id> <open|resolved> '<response>'`
   - If implementation is incomplete or has open points, leave the thread open.

**CRITICAL** Always use the **thread id** (not comment ids). Shell-escape all responses.
**CRITICAL** In the sub-agent spawn, give the thread id and a brief one line **subject** of those threads.
**CRITICAL** Always be explicit o the sub-agent regarding plan or implement: should they only propose a plan, or should they implement the fix? When in doubt if the user has approved or not, prefer to fallback to proposing a plan.
**CRITICAL** Do not give a response for the sub-agent to reply. Tell him how to call the command, but let him build the appropriate response based on his findings.

## Reactions

Look into the reactions of comments, to give you hints on what to do:
- Approved: you can proceed with implementing the described changes
- Explain: rephase your explanation in more detail than previously
- Rejected: the user has rejected your plan. Think of alternative ways of achieving the original request.
- Paused: Ignore this conversation for now. When this is present on any comment in a thread, this applies to the entire thread and takes precedence over other commands.
- Learn: Can be ignored in this situation.
