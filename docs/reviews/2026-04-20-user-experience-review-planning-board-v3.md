# Planning Board User Experience Review v3

Date: 2026-04-20  
Perspective: Product Owner using the board to plan real Epic work

## Overall verdict

This version is better. I understand the main action faster, and I am less likely to freeze before touching the board.

But it still does not feel truly fluid. It feels like a smarter planning form, not yet like a fast planning workspace.

Confidence after this pass: **medium**

---

## Workflow simulation

### 1. First impression (speed)

I now understand the primary move path quickly:

- there is one obvious action
- it is called **Move Epic**
- it tells me what the default behavior is

That is a real improvement. I no longer stop first to compare several similar scheduling actions.

I still pause, but later than before. The initial hesitation has moved from **"what button should I use?"** to **"how much should I move this by?"**

### 2. Shape a plan quickly

Moving several Epics with the default action feels more natural than before.

What works:

- I can spot the main action immediately
- I can use the same move pattern repeatedly
- the board tells me that downstream work will shift automatically

What still slows me down:

- I still type a number, apply, wait, inspect, repeat
- fast iteration still feels like a sequence of careful edits rather than shaping a plan in flow
- I still need to look back at the board after each move to confirm whether the downstream effect matches what I expected

I act faster than before, but not instantly.

### 3. Introduce complexity

Parallel work still feels clear:

- **Create parallel work** is understandable
- **Return to main plan** is understandable
- derived lanes still feel like the system helping me instead of asking me to do admin work

Where complexity returns:

- once overlap or compression appears, I still need to think about whether I should use the default move again, open **Advanced options**, or switch to parallel work
- the board explains outcomes better than before, but it still does not make repair actions feel effortless

### 4. Iterate aggressively

The board is noticeably better under repeated use because the default action removes a recurring choice.

That said, aggressive iteration still feels step-by-step:

1. choose Epic
2. type delta
3. click
4. inspect result
5. repeat

The experience is more direct than before, but not yet fast enough to feel like sketching a plan.

### 5. Make and fix mistakes

Mistake correction is improved by the clearer move default and the stronger changed/shifted feedback.

The good part:

- I can usually see what changed
- I can usually see what shifted
- I can reverse course with another explicit move

The bad part:

- recovery still feels slightly careful rather than carefree
- I still do not feel fully safe making bold experimental changes in quick succession
- **Reset** remains too heavy psychologically, even if I do not need it often

### 6. Handle drift

The updated TFS action is easier to approach than older terminology, but it still creates hesitation.

My reaction as a Product Owner is still:

- "Am I only updating reported dates?"
- "Am I fixing display drift or changing something important?"
- "Do I need to do this now, or is it an operational cleanup step?"

I trust it more than before, but I still do not click it casually.

---

## Answers to the evaluation criteria

### 1. Do you act immediately or still pause to think?

I act faster, but I still pause.  
The pause is no longer about choosing among too many similar scheduling actions. Now it is mostly about deciding the size of the move and checking whether the downstream effect is worth it.

### 2. Where does the system still slow you down?

- repeated numeric entry
- inspect-after-every-change behavior
- needing to open **Advanced options** when the default move is close but not quite right
- interpreting drift/update actions as operational rather than planning actions
- correcting several Epics one by one during rapid reshaping

### 3. Where do you hesitate before clicking?

- before opening **Advanced options**, because opening it means I am leaving the easy path
- before using **Update TFS dates**, because it still sounds consequential
- before choosing between another default move and **Change priority order**

### 4. Does the default “Move Epic” behavior match your expectation?

Yes.  
It matches the most natural expectation: if I move an Epic in a real plan, work after it usually needs to move too.

This is the right default.

### 5. Do you feel in control during rapid iteration?

Mostly, but not fully.

I feel more in control of the direction of the plan. I do not feel fully in control of speed. The board is still deliberate.

### 6. What still feels like too many steps?

- moving several Epics in a row
- making one mistake and then rebalancing the nearby sequence
- switching from sequential planning to parallel planning and then cleaning up the result
- deciding whether a correction is a move problem or a priority problem

### 7. Where does the system feel fast?

- first action on an Epic
- repeated use of the default move
- creating parallel work
- spotting what changed after a move

### 8. Where does it still feel mechanical?

- numeric delta entry on every timing change
- per-Epic adjustment loops
- open advanced / make one special correction / close mentally back to the default path
- operational cleanup actions around TFS sync

---

## Top 5 remaining friction points

1. **Rapid iteration still depends on repeated number-entry cycles.**  
   The default action is clearer, but the interaction rhythm is still type, click, inspect, repeat.

2. **The board still makes me verify consequences after every move.**  
   It shows more feedback now, but not enough to remove the need for visual checking.

3. **Advanced options still feel like a decision branch.**  
   They are correctly hidden, but the moment I need them, I am back in “which mechanism do I mean?” thinking.

4. **TFS update behavior still feels operational and slightly risky.**  
   I understand it better, but I still hesitate because it sounds like a side-effectful housekeeping step.

5. **Priority change versus movement still overlaps in the user’s mind.**  
   When reshaping a plan quickly, I still have to stop and ask whether I want to move timing or change sequence.

## Top 3 improvements compared to the previous version

1. **The board now gives me one obvious default action instead of forcing an upfront strategy choice.**
2. **The “Your plan” vs “Calculated schedule” split is easier to read quickly than the old time labels.**
3. **Changed/shifted feedback is more useful and easier to scan during iterative planning.**

## 3 moments where I still hesitated

1. After moving one Epic twice, I hesitated before deciding whether the next correction should be another default move or an order change.
2. After creating parallel work, I hesitated on the cleanup step because the next shaping action still required conscious choice.
3. When I saw the TFS update action, I hesitated because it still feels more serious than routine.

## 2 things that still feel unnecessarily complex

1. **Fixing a local planning mistake that spills into nearby work.**  
   It is possible, but it still feels like a sequence of controlled corrections rather than a quick adjustment.

2. **Knowing when to switch from the default move path into Advanced options.**  
   The default is better, but the handoff to non-default actions still costs thinking effort.

## 1 moment where the system felt fast and natural

I picked an Epic, entered a sprint delta, clicked **Move Epic**, and immediately understood the result from the changed/shifted feedback without first choosing among multiple scheduling actions. That is the first moment the board felt like it was helping me plan instead of asking me to operate it.

---

## Bottom line

This version is genuinely better for real planning work.

The main gain is not that the board became simpler. The main gain is that it removed one repeated planning tax: I no longer have to choose the editing mechanism before I can start shaping the plan.

But the board still feels more deliberate than fluid. It is faster now, not fast yet.
