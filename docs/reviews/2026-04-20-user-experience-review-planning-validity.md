# Planning Board User Experience Review — Planning Validity

Date: 2026-04-20  
Perspective: Product Owner using the planning board to decide whether a multi-Epic plan is actually achievable

## Overall verdict

The board is now good at showing **what changed**. It is still weak at telling me whether the resulting plan is **credible**.

I can shape the structure of a plan. I cannot honestly defend the realism of that plan from this board alone.

Confidence after this pass: **medium-low**

---

## Workflow simulation

### 1. Build a plan

I start by shaping several Epics across a run of sprints, then create some parallel work where the sequence becomes too long.

At this stage the board feels competent:

- I can move Epics quickly
- I can see which Epics changed or shifted
- I can create parallel work without doing manual lane bookkeeping
- I can understand the resulting structure better than before

What I cannot tell yet is whether the plan is merely arranged neatly or actually achievable.

The board tells me:

- when work moved
- where work sits
- when overlap was introduced

It does **not** tell me:

- whether the team has enough delivery capacity
- whether this level of parallelism is realistic
- whether the finish dates are based on anything that has ever happened before

So I can build a plan, but I am building it with structural confidence, not delivery confidence.

### 2. Evaluate the plan

This is the moment that matters: I stop editing and ask, **“Can we actually deliver this?”**

The board gets much weaker here.

The schedule looks orderly, but orderly is not the same as believable.

What I want at this point is evidence:

- how much work normally fits in one sprint
- whether similar Epic mixes have succeeded before
- whether this amount of overlap is healthy or reckless
- whether the projected finish dates are aggressive, normal, or fantasy

Instead I mostly get placement, lane structure, and change explanation.

That helps me understand the current board state. It does not help me trust it.

### 3. Stress the plan

I then compress the plan, extend it, and add more parallel work.

This is where my confidence drops fastest.

When I compress timelines:

- the board shows the structural result clearly
- the board does not tell me when I crossed from ambitious into unrealistic

When I extend timelines:

- the board becomes tidier
- the board still does not tell me whether I now have enough slack or just more empty space

When I add more parallel work:

- the board can represent it
- the board does not tell me whether the team can support that concurrency

So the board is good at showing **how the plan changed** under stress. It is bad at telling me whether the stressed plan is still believable.

### 4. Reflect on confidence

By the end of the exercise, I trust the board as a **planning structure editor**.

I do not trust it yet as a **planning validity judge**.

If I had to present this plan upward and defend the dates, I would still leave the board and look for external confidence signals before committing to it.

That is the real issue: the board reduces ambiguity about layout, but not enough ambiguity about feasibility.

---

## Answers to the evaluation criteria

### 1. Do you feel confident this plan is achievable?

Not fully. I feel confident the structure is coherent. I do not feel confident the delivery promise is credible.

### 2. What makes you doubt the plan?

- I cannot see whether sprint loading is realistic
- I cannot judge whether the amount of parallel work is within team capacity
- I cannot compare this schedule to past delivery performance
- the board shows dates and placement without enough evidence behind them

### 3. What information are you missing (if any)?

- delivery capacity by sprint
- expected throughput or velocity context
- forecast confidence or risk level
- historical performance against similar plans

### 4. Do you look for signals like velocity, capacity, or past performance?

Yes. Immediately. Without them I am only looking at a shaped schedule, not a validated plan.

### 5. At what point do you stop trusting the plan?

I stop trusting it when I create enough overlap or compression that success depends on assumed capacity rather than visible evidence.

### 6. Does the board help you judge realism, or only structure?

Mostly structure. It helps me judge sequencing, overlap, and local consequences. It does not help enough with realism.

### 7. What would increase your confidence?

- explicit signals about likely delivery capacity
- evidence from past performance
- a clear sense of whether the current schedule is conservative, normal, or aggressive

---

## Top 5 gaps in judging plan realism

1. **No visible capacity signal.**  
   I can see when work overlaps, but not whether the team can actually sustain that overlap.

2. **No velocity or throughput context.**  
   The board shows a schedule without telling me how much work the team usually finishes in comparable sprints.

3. **No forecast confidence indicator.**  
   I do not know whether the dates shown are strong, weak, optimistic, or speculative.

4. **No historical grounding.**  
   Nothing tells me whether plans like this have succeeded before or usually slip.

5. **No stress threshold marker.**  
   I can compress the plan repeatedly, but the board never clearly tells me when the plan stops being believable.

## Top 3 things that give confidence

1. **The board now explains impact clearly.**  
   I understand what changed and what moved without scanning the whole board.

2. **Parallel structure is explicit.**  
   I can see when work has become concurrent instead of guessing from visual collisions.

3. **Changed and affected Epic feedback is concrete.**  
   The board gives enough local explanation to trust that the structural outcome is internally consistent.

## 3 moments where I doubted the plan

1. **After compressing several Epics into fewer sprints.**  
   The board showed the new shape, but gave me no proof that the team could actually absorb that compression.

2. **After adding more parallel work to rescue the finish date.**  
   The board made the schedule look possible on screen, but I had no reason to believe the team could execute that level of concurrency.

3. **When the plan looked neat after extension.**  
   I could not tell whether I had created realistic slack or just spread uncertainty across more sprints.

## 2 signals I wished were present

1. **Capacity per sprint**
2. **Past delivery performance / velocity trend**

## 1 sentence: do you trust this plan or not?

I trust the board to show me a coherent plan shape, but I do **not** trust it on its own to prove that the plan is achievable.

---

## Bottom line

This board now gives me much better confidence in **what the plan is**.

It still does not give me enough confidence in **whether the plan will hold**.

That distinction matters. A Product Owner does not just need a clean schedule. A Product Owner needs enough evidence to believe the schedule is real.
