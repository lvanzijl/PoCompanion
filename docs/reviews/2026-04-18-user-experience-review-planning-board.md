# Planning Board User Experience Review

Date: 2026-04-20
Perspective: Product Owner planning real Epic work across multiple sprints

## Overall verdict

This board looks serious, but it does not feel immediately usable.

The core idea is strong: place Epics on a sprint timeline, let the system derive parallel tracks, keep the plan durable, and surface drift. That part is valuable.

The problem is that the board talks like an internal planning engine instead of a planning tool for humans. I can eventually understand it, but I have to work too hard to trust it.

Confidence level after first pass: **medium-low**

---

## Workflow simulation

### 1. First impression

What I understand immediately:

- This is a product-scoped planning board.
- I am planning Epics across sprints.
- The board can derive tracks automatically.
- I can reload or reset the session.

What is confusing immediately:

- “durable session-backed state”
- “durable or recovered base session”
- “changed”, “affected”, “recovered”, and “drifted” all appearing as separate concepts
- the difference between **planned start**, **computed start**, and **derived end**
- whether this board is a safe sandbox or a live authoring surface

The first screen makes me stop and decode terminology before I can plan.

### 2. Create a plan

Placing Epics sequentially makes sense once I read the cards:

- roadmap order
- planned start
- duration
- track position

That gives me enough to understand the current sequence.

The problem is speed. To make a simple change, I have to choose between:

- Move by sprint
- Adjust spacing before
- Shift plan suffix
- Reorder roadmap sequence

Those are not four obviously different user intents. They are four similar-looking operations with different consequences. That slows planning down fast.

### 3. Introduce complexity

“Run in parallel” is the strongest interaction on the page.

Why it works:

- the label is plain language
- I immediately understand the intent
- the system derives the track instead of asking me to manage lane numbers

That is a good surprise.

The hesitation starts right after that:

- how much of the downstream plan changed?
- did duration assumptions stay intact?
- should I use **Return to main**, **Shift plan suffix**, or **Adjust spacing** to repair the shape afterward?

The board shows changed and affected highlights, which helps, but I still have to reconstruct the impact mentally.

### 4. Make a mistake

Recovery exists, but it does not feel lightweight.

- **Reset session** sounds global and destructive.
- **Reload board** sounds safe, but it is not obvious whether it preserves my current working intent.
- per-Epic corrections exist, but some are named too mechanically.

I do not feel comfortable experimenting freely because the recovery language suggests I might lose the plan or overwrite something external.

### 5. Handle drift

The system is trying to be honest about drift, which is good.

But trust breaks on the wording:

- “drifted epic”
- “recovered”
- “reconcile TFS projection”
- “without changing the intent itself”

As a Product Owner, my reaction is: **which thing is the real plan?**

My mental model becomes:

1. there is an internal planning intent
2. there is a TFS projection written from it
3. those can diverge
4. reconcile pushes one back toward the other

That is workable, but not comfortable. I do not fully trust a button that sounds like it repairs data I cannot see directly.

### 6. Iterate

After multiple changes, I feel partly in control of the layout, but not fully in control of the consequences.

Why:

- the board does show status
- changed and affected markers do help
- validation and diagnostics are visible

But:

- too much state vocabulary competes at once
- several operations require translation before action
- the board explains system state better than planning intent

End result: I can use it, but I do not feel relaxed using it.

---

## Answers to the evaluation criteria

### 1. What feels intuitive?

- viewing Epics on a sprint grid
- seeing duration and start position on each Epic
- using **Run in parallel**
- using **Return to main**
- seeing changed and affected feedback after a mutation

### 2. What slows you down?

- heavy terminology
- too many similar action types
- numeric-input-plus-apply interaction on nearly every change
- needing to interpret multiple status systems at once
- lack of a simple “what will happen if I do this?” cue

### 3. Where do you hesitate?

- before using **Shift plan suffix**
- before using **Adjust spacing before**
- before deciding whether to reorder or move
- before using **Reset session**
- before using **Reconcile TFS projection**

### 4. Where do you NOT trust the system?

- whenever internal plan state and TFS projection are described as separate truths
- whenever drift is reported without a very plain explanation of impact
- whenever a change appears to persist immediately
- whenever recovery wording suggests normalization or reconciliation happened automatically

### 5. What actions feel unclear or risky?

- **Shift plan suffix**
- **Adjust spacing before**
- **Reorder roadmap sequence**
- **Reset session**
- **Reconcile TFS projection**

### 6. What mental model did you form of how it works?

I formed this model:

- I author Epic intent using start sprint, ordering, spacing, and duration
- the board computes the actual schedule shape from that intent
- parallel tracks are generated automatically
- the system remembers the planning session
- TFS stores projected dates derived from the plan
- drift means TFS no longer matches the board’s internal plan
- reconcile rewrites TFS to match the board again

This model is understandable, but it takes too long to build.

### 7. Where does the system surprise you?

#### Good surprises

- automatic track derivation
- clear **Run in parallel** and **Return to main** actions
- changed/affected highlighting after a mutation
- visible diagnostics instead of silent failure

#### Bad surprises

- how much internal terminology is exposed
- how many action types exist for basic schedule editing
- how risky reset/reconcile language sounds
- how hard it is to know which state is authoritative at a glance

---

## Top 5 friction points

1. **The board speaks in system language instead of planning language.**  
   I should not need to decode “durable”, “recovered”, “drifted”, and “projection” just to move work.

2. **Too many similar actions compete for the same scheduling problem.**  
   Move, spacing, shift, reorder, and parallelization are not separated clearly enough in the user’s mind.

3. **Trust is fragile because persistence and recovery are not framed simply.**  
   I cannot tell instantly whether I am editing a safe working plan, a persisted shared plan, or both.

4. **The board exposes too many state signals at once.**  
   Status chips, validation issues, diagnostics, changed/affected markers, recovery labels, and drift labels create cognitive clutter.

5. **Iteration feels slower than it should.**  
   Repeated numeric entry plus Apply is precise, but it is not fast for exploratory planning.

## Top 3 strengths

1. **Automatic track derivation is exactly the right idea.**
2. **Changed and affected highlighting gives useful immediate feedback.**
3. **The board makes operational problems visible instead of hiding them.**

## 3 moments where I felt uncertainty

1. On first load, when I had to understand whether the board was showing a live plan, a cached plan, or a recovered plan.
2. After introducing overlap, when I had to decide whether the next corrective action was move, spacing, shift, or return to main.
3. When drift appeared, because **Reconcile TFS projection** sounds consequential but not fully explainable from the page itself.

## 2 things I would change immediately

1. **Replace engine language with plain planning language everywhere the user makes decisions.**  
   The board should explain effects in terms of sequence, overlap, and committed plan confidence.

2. **Reduce the action surface into fewer, clearer scheduling intents.**  
   I should not have to choose among several mechanically named actions to solve one planning problem.

## 1 thing I would NOT change under any circumstance

**Do not make users choose track numbers manually.**  
Automatic derived tracks are one of the few places where the product is clearly smarter than the user, and that is exactly how it should stay.

---

## Bottom line

The planning board has a credible planning engine behind it, but the user experience still feels too technical.

I can see the value. I do not get fast confidence.

Right now, this is a board I would use carefully, not a board I would use fluidly.
