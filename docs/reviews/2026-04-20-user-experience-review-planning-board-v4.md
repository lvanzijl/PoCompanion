# Planning Board User Experience Review v4

Date: 2026-04-20  
Perspective: Product Owner shaping a real plan across multiple Epics and sprints

## Overall verdict

This is the first version that starts to behave like a planning workspace instead of a controlled editing form.

The new quick moves change the rhythm. I no longer have to commit to typing before I can try a timing change. I can act, look, act again, and keep going on the same Epic.

That said, the board is still not fully fluid. It is faster, but it still makes me notice the tool during more complex reshaping than I want.

Confidence after this pass: **high**

---

## Workflow simulation

### 1. Immediate interaction

I start acting much faster now.

Before, I had to decide the move size before I could even begin. Now I can hit `+1` or `-1` immediately and see what happens. That changes the posture from cautious setup to active shaping.

Do I still hesitate? Yes, but later.

- I no longer hesitate at the start of the interaction
- I sometimes hesitate after the first move, when deciding whether to keep nudging or switch to a more specific action
- the first click now feels cheap enough to try

### 2. Rapid reshaping

This is where the board improved the most.

Moving several Epics in succession with quick controls feels materially faster because the interaction loop is now:

1. click
2. inspect local feedback
3. click again

instead of:

1. type
2. click
3. inspect
4. type again

I stay in flow longer when reshaping nearby Epics. The board still slows me down when I need to compare consequences across more than one card, but the raw adjustment loop is much better.

### 3. Continuous iteration

Adjusting the same Epic repeatedly now feels natural enough.

The best moment is simple:

- `+1`
- `+1` again
- `Repeat +1 sprint`

That sequence finally feels like I am nudging a plan instead of programming a board.

Switching to another Epic is reasonably smooth because the same quick controls are waiting there. The flow breaks only when I need to interpret the broader board shape again, not when I need to remember how to operate the control.

### 4. Error correction

Mistake correction is much faster now.

If I overshoot by one sprint, I can immediately hit the opposite quick action and recover without resetting context. That matters a lot. Errors now feel reversible at the same speed as the original move.

What still slows recovery:

- I still need to visually confirm the downstream effect
- if the mistake created a more structural problem, I still have to decide whether to keep nudging or switch to another action

### 5. Complex shaping

This is still mixed.

Introducing overlap and creating parallel work still feels like one of the smarter parts of the board. The system helps instead of asking me to do bookkeeping.

But cleaning up timing after that still feels more mechanical than it should:

- I create the parallel lane
- I read the changed shape
- then I return to deliberate correction mode

The board is faster inside a move loop, but once the plan shape becomes more complex, I still become very aware that I am operating explicit controls.

### 6. Drift handling

`Update TFS dates` is understandable, but it still interrupts planning flow.

Not because it is unclear, but because it feels like leaving planning mode and entering maintenance mode. I understand it, but I do not want to touch it casually in the middle of shaping work.

It is not confusing anymore. It is just not part of the same flow.

---

## Answers to the evaluation criteria

### 1. Do you stay in flow or still break between actions?

I stay in flow during quick timing adjustments on the same Epic and across nearby Epics.  
I still break when I need to interpret downstream consequences, open non-default actions, or deal with sync-related cleanup.

### 2. Where does interaction still feel slow?

- reading the wider board after each meaningful reshape
- deciding when the default move is no longer the right tool
- cleaning up after introducing parallel work
- switching mental mode to TFS update actions
- comparing local card feedback with overall board consequences

### 3. Do you still “think before acting” or mostly “act and adjust”?

Mostly **act and adjust** for small timing moves.  
Still **think before acting** once the change affects plan structure rather than just timing.

### 4. How fast can you correct mistakes?

Fast for local mistakes.  
One wrong nudge is easy to reverse immediately.  
A mistake that changes nearby schedule shape is still recoverable, but it slows down because I have to inspect more before continuing.

### 5. Does the tool get out of your way or stay visible?

It gets out of the way during repeated quick moves.  
It stays visible during structural cleanup, advanced corrections, and sync handling.

### 6. Where does it still feel mechanical?

- when I need to switch from nudging to deciding among different correction types
- when I have to read multiple cards to confirm impact
- when a planning action turns into a cleanup sequence
- when sync handling appears next to planning actions

### 7. Where does it feel natural?

- first move on an Epic
- repeated `+1` / `-1` timing nudges
- correcting a small overshoot immediately
- continuing on another Epic with the same quick controls
- introducing parallel work and seeing the lane derive automatically

---

## Top 5 remaining friction points

1. **Board-wide consequence reading is still heavier than the move itself.**  
   The click is fast now, but understanding the full impact still costs attention.

2. **The handoff from quick nudging to non-default actions still breaks flow.**  
   The moment I need anything beyond the default move, I slow down noticeably.

3. **Parallel cleanup is still more deliberate than fluid.**  
   Creating parallel work feels smart; refining the resulting shape still feels procedural.

4. **Sync handling still feels like an interruption.**  
   `Update TFS dates` makes me leave planning mode mentally, even if I understand it.

5. **The board still asks for visual verification after each meaningful reshape.**  
   I trust local feedback more now, but not enough to stop scanning.

## Top 3 moments where the system felt fast

1. Clicking `+1 sprint` on an Epic immediately after opening the board without typing anything first.
2. Repeating the same move on one Epic several times in a row and staying on the same control surface.
3. Overshooting by one sprint and fixing it instantly with the opposite quick action.

## 3 moments where flow was interrupted

1. After a few quick nudges, when I had to stop and inspect the broader board shape instead of continuing locally.
2. After creating parallel work, when cleanup returned me to deliberate correction mode.
3. When `Update TFS dates` appeared as the next sensible housekeeping step and broke planning momentum.

## 2 things that still slow down iteration

1. **Needing to re-evaluate the whole visible plan after a local move.**
2. **Deciding when to leave the fast default move path for another explicit action.**

## 1 sentence: does this feel like shaping a plan or operating a tool?

This now feels more like **shaping a plan** during local timing work, but it still feels like **operating a tool** during structural cleanup and sync-related steps.

---

## Final assessment

The quick controls are the first change that meaningfully lowers planning friction instead of just clarifying it.

The board is now fast enough to encourage experimentation at the local Epic level. That is a real threshold improvement.

But the experience still loses fluency when the plan becomes structurally interesting. The system is no longer blocking flow at the start; it is blocking flow later, during interpretation and cleanup.
