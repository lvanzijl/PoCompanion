# Planning Board User Experience Review v2

Date: 2026-04-20  
Perspective: Product Owner planning real Epic work across multiple sprints

## Overall verdict

This is much closer to a usable planning tool now.

I understand the board faster, I trust it more quickly, and I no longer feel like I am reading internal system vocabulary just to do normal planning work.

That said, it is still not fluid. It is understandable, but not yet fast. The board feels safer than before, but it still makes me think too hard during iteration.

Confidence level after realistic use: **medium-high**

---

## Workflow simulation

### 1. First impression

What I understand immediately:

- this board defines the plan
- TFS receives dates for reporting
- I am planning Epics across sprints
- parallel work is allowed, but derived automatically
- the board prefers explicit actions over drag-and-drop

What is still confusing immediately:

- the difference between **Chosen start** and **Scheduled start**
- whether **Change priority order** is mainly about sequencing or can also visibly reshape timing
- how much the board will recalculate after each action before I touch anything

The first impression is no longer hostile. It is reasonably clear. I can start thinking about planning instead of decoding terminology.

### 2. Create a plan

Sequential planning works.

I can look at the Epic cards, see start, finish, duration, lane, and order, then make a usable mental model of the current plan. The board now talks in planning language, which matters more than it sounds.

What slows me down is not understanding. It is action selection.

I still have to pause and ask:

- do I want to move only this Epic?
- move this and shift following work?
- move everything after this?
- change priority order?

These are much better labels than before, but they are still several separate levers for what feels like one planning intent: “reshape the schedule.”

### 3. Introduce complexity

Creating overlap and then using **Create parallel work** is one of the best moments in the whole experience.

Why it works:

- the action is direct
- the consequence is explained
- the lane is derived automatically
- the resulting board shape is easy to read

This is where the product feels smart.

The hesitation returns when I want to refine the result. Once parallel work exists, I still need to decide which of the scheduling actions best repairs or improves the plan shape.

### 4. Make a mistake

I feel safer making mistakes than before.

Why:

- **Undo all changes** is understandable
- **Reload from saved plan** sounds safe, not threatening
- changed and affected states show me what I just disturbed

But I still would not call experimentation frictionless. The board persists planning, so every action still feels consequential. The wording is better, but the interaction model still pushes me to think carefully before touching anything.

### 5. Handle drift

This is improved a lot.

“Out of sync with TFS” is plain enough.  
“Update TFS dates” is understandable.  
The board now tells me the plan lives here and TFS is reporting output.

That is the right trust model.

Where trust is still incomplete:

- I can see that something is out of sync, but I still need to infer the operational consequence
- I understand what **Update TFS dates** does, but I still want slightly stronger reassurance that it does not alter the plan itself

I trust this area more than before, but not fully.

### 6. Iterate

After multiple changes, I feel mostly in control, not fully in control.

Why the experience works:

- changed and blocking states stand out
- secondary and tertiary signals no longer dominate
- the board tells a clearer story
- explicit operations make results feel deliberate

Why it still gets tiring:

- every adjustment is still a deliberate micro-decision
- several actions remain close together conceptually
- numeric entry is precise, but not quick

This is now a board I can use confidently, but not one I can use fast.

---

## Answers to the evaluation criteria

### 1. What feels intuitive?

- the authority message at the top of the board
- reading Epics on a sprint grid
- derived parallel lanes
- **Create parallel work**
- **Undo all changes**
- changed vs affected feedback

### 2. What slows you down?

- choosing among several schedule-editing actions
- repeated numeric entry for small planning moves
- interpreting the difference between sequencing actions and timing actions
- understanding **Chosen start** versus **Scheduled start**
- checking consequences across multiple cards after each move

### 3. Where do you hesitate?

- before deciding between **Move this and shift following work** and **Move everything after this**
- before using **Change priority order** when timing is also already visible
- before using **Update TFS dates** when the board already looks correct

### 4. Where do you NOT trust the system?

- when I need to infer exactly how much downstream schedule shape changed from one action
- when TFS sync status appears without a very concrete explanation of business impact
- when the board shows both planning checks and sync details and I need to judge which one actually matters first

### 5. What actions feel unclear or risky?

- **Move this and shift following work**
- **Move everything after this**
- **Change priority order**
- **Update TFS dates**

### 6. What mental model did you form of how it works?

I formed this model:

- this board is the planning source of truth
- I place or adjust Epic intent using start, order, and schedule-shaping actions
- the board derives the resulting schedule and parallel lanes
- my changes persist
- TFS receives reported dates from this plan
- if TFS is out of sync, I can push the board’s plan back out to TFS

This is finally a workable mental model without translation overhead.

### 7. Where does the system surprise you?

#### Good surprises

- the clarity of the authority message
- how understandable **Create parallel work** feels
- derived lanes staying automatic
- changed and affected feedback remaining visible but not noisy

#### Bad surprises

- how many scheduling actions still exist for one board
- how slow iterative refinement feels
- how subtle the difference is between some timing and sequencing controls

---

## Top 5 friction points

1. **Too many schedule-editing actions still compete for attention.**  
   The labels are better, but I still need to stop and choose among several similar intents.

2. **The board is clear, but not quick.**  
   Numeric inputs plus per-action buttons make precise planning possible, but they slow iterative experimentation.

3. **Chosen start vs Scheduled start still needs mental translation.**  
   I can figure it out, but it is not instant.

4. **Downstream impact still requires visual reconstruction.**  
   Changed and affected help, but I still have to scan multiple cards to feel certain about consequences.

5. **TFS sync actions are understandable, but still slightly tense.**  
   I know what the action means now, but I still pause before using it because it sounds operationally important.

## Top 3 strengths

1. **The board now clearly tells me what is authoritative.**
2. **Automatic parallel lane derivation remains the smartest part of the experience.**
3. **State hierarchy is much better; changed and blocking issues now stand out appropriately.**

## 3 moments where I felt uncertainty

1. When I had to choose between moving one Epic, moving one plus following work, or moving everything after it.
2. When I saw both **Chosen start** and **Scheduled start** and had to remember which one represented my intention versus the resulting schedule.
3. When the board appeared correct but I still had to decide whether **Update TFS dates** was necessary right now.

## 2 things I would change immediately

1. **Reduce the visible schedule-editing surface further.**  
   I want fewer planning decisions about mechanism and more direct decisions about outcome.

2. **Explain the difference between chosen timing and resulting timing more bluntly.**  
   That distinction is central to the board, and it should be effortless to read.

## 1 thing I would NOT change under any circumstance

**Do not stop deriving parallel lanes automatically.**  
That is one of the few places where the system removes work instead of creating it, and it materially improves planning confidence.

---

## Bottom line

This board now feels credible as a planning tool, not just as a planning engine UI.

I understand it faster. I trust it more. I can see the value without fighting the vocabulary.

But I still do not feel fast.

The remaining problem is no longer language. It is interaction load. The board helps me think, but it still asks me to make too many micro-decisions while shaping a plan.
