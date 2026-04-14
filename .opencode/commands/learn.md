---
description: Extract lessons learned from all review threads
---

Past review threads:

!`./.localea/reviews/list.sh`

For each thread, extract lessons learned and categorize them as:
- **General**: Applicable across any project (patterns, best practices, anti-patterns)
- **Project**: Specific to this codebase (architectural decisions, conventions, domain knowledge)

First make a summary of the learnings for yourself. Give especial importance to learnings from comments with the `"learn"` user reaction.
Think thoroughly a second time through each individual learning point, and justify why they are important enough to be included in the final list. Avoid learning very specific or niche topics, platitudes, or other kinds of information of low importance.

Finally, present a summary grouped by category. Do not spawn sub-agents — analyze and synthesize directly.

If the user then instructs you to, save the knowledge in the appropriate places. Usually `~/.config/opencode/AGENTS.md` for general knowledge, and `AGENTS.md` for project specific. But respect the project conventions if they differ.
