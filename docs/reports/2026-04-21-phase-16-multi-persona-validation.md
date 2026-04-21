# Phase 16 multi-persona validation

## Scope

- VALIDATED: current Phase 15 planning intelligence behavior only.
- DID NOT IMPLEMENT: no code changes, no redesign, no new data sources.
- Evidence used:
  - `PoTool.Client/Models/ProductPlanningSprintSignals.cs`
  - `PoTool.Tests.Unit/Models/ProductPlanningSprintSignalFactoryTests.cs`
  - `PoTool.Client/Pages/Home/PlanBoard.razor`
  - `PoTool.Client/Models/PlanningBoardImpactSummary.cs`
  - `docs/reports/2026-04-21-phase-15-signal-calibration.md`

## Validation method

This validation used three realistic personas against the current signal logic and visible Plan Board presentation.

Simulated scenarios:

1. **Baseline plan**
   - sequential work
   - low overlap
   - near-term sprints
2. **Aggressive compression**
   - multiple Epics landing together
   - work pulled forward
   - overlap introduced
3. **Parallelization**
   - additional tracks introduced
   - same sprint load spread across more lanes
4. **Long-horizon planning**
   - stable far-future sprints
   - minor distant changes
   - repeated small reshaping
5. **Passive reading**
   - signals read as a stakeholder with minimal interaction
   - only labels, chips, tooltips, and latest-impact summaries considered

## What the personas actually see

### Risk

- Classifies to `Low`, `Medium`, `High`.
- Driven by:
  - active Epic count
  - active track count
  - forward-shift count
  - overlap pair count
- Uses board-relative baselines for load, track spread, and overlap pressure.

### Confidence

- Classifies to `High`, `Medium`, `Low`.
- Driven by:
  - normalized horizon distance
  - changed/affected Epic count
  - structure change
  - forward-shift count
- Uses gradual distance decay: `distanceRatio * 1.9`.

### Explanation layer

- UI shows:
  - heat color = risk
  - heat opacity = confidence
  - labels
  - up to 4 explanation chips
  - tooltip text
  - latest-impact delta summaries

---

## Persona 1 — execution-focused product owner

### Scenario used

- Start from a realistic sequential plan.
- Compress several Epics into the same sprint window.
- Add parallel work.
- Observe whether the sprint heat feels like real delivery pressure.

### Where this persona trusts the signals

- Trust is highest when obvious compression is present:
  - many active Epics
  - 3+ active tracks
  - visible overlap
  - forward pull-in after reshaping
- `High` risk generally aligns with intuitive delivery strain when multiple structural pressures stack together.
- The sprint-level framing is useful because it answers the execution question directly: “Which sprint is dangerous now?”

### Where this persona doubts the signals

- Chronic dense planning can be normalized away because risk compares a sprint against the board’s own average load.
- If the whole plan is crowded, a crowded sprint can still look merely normal.
- Two-track parallelism can feel undercalled when it is operationally unrealistic but does not cross the higher structural thresholds.

### What feels incorrect or misleading

- A uniformly overcommitted board can look calmer than it should because the baseline moves upward with the crowding.
- Parallel work realism is treated mostly as a count problem, not as a planning plausibility problem.
- Delta summaries highlight only the biggest signal change, so distributed pressure across several sprints can be visually larger than the text summary suggests.

### What would make this persona trust it more

- More consistent alignment between “board is broadly overloaded” and the visible sprint heat.
- Clearer indication when normalization is masking a plan that is globally aggressive.
- More explicit distinction between “more lanes” and “realistically executable lanes.”

### Persona verdict

- **Directional but incomplete.**
- Good at finding concentrated sprint strain.
- Weaker at calling out systemic overcommitment across the whole board.

---

## Persona 2 — strategic planner

### Scenario used

- Extend the plan far into the future.
- Apply small distant changes.
- Observe whether confidence fades naturally and whether repeated minor reshaping creates instability.

### Where this persona trusts the signals

- Confidence decay is gradual, not binary.
- Near-term steady sprints remain `High`, which feels correct.
- Minor far-future changes do not instantly collapse confidence to `Low`, which avoids noise.

### Where this persona doubts the signals

- Distance alone can only move stable far-future sprints to `Medium`, never to `Low`.
- That means long-range uncertainty is moderated heavily unless additional instability exists.
- A long roadmap can therefore appear more certain than a strategic planner would intuitively feel.

### What feels incorrect or misleading

- The current decay curve is gentle enough that much of the horizon can still look `High`.
- Repeated small changes only matter when they surface through changed/affected counts or structure deltas in the current board comparison.
- Confidence reflects present-board instability, not the broader sense of “this plan has been revised often over time.”

### What would make this persona trust it more

- Stronger visual distinction between “near-term trustworthy” and “far-future provisional,” even when the structure is currently calm.
- More confidence that repeated minor reshaping cannot hide behind a stable final snapshot.
- Clearer wording that confidence is about plan stability, not delivery certainty.

### Persona verdict

- **Calm and usable, but somewhat optimistic.**
- The decay feels natural.
- The far future still reads more certain than strategic intuition would prefer.

---

## Persona 3 — skeptical stakeholder

### Scenario used

- Inspect a completed plan with minimal interaction.
- Read the heat cards, labels, chips, tooltips, and latest-impact text without studying the formulas.
- Challenge whether the signals feel explainable and credible.

### Where this persona trusts the signals

- Risk and confidence are separated visibly and linguistically.
- Plain-language labels are easy to parse.
- Tooltips and chips make the system feel less opaque than a bare red/yellow/green heatmap.

### Where this persona doubts the signals

- The explanations are still heuristic summaries, not direct evidence.
- Tooltips often say “because several Epics land together, parallel work is elevated, or recent moves compressed the plan,” which can feel broad rather than specific.
- The system does not expose the exact trigger behind a classification in the UI.

### What feels incorrect or misleading

- Explanations can feel post-hoc because they restate categories rather than showing concrete proof.
- Some contributing factors are hidden by chip prioritization:
  - `Work pulled forward` suppresses `Overlap pressure`
  - chip count is capped at 4
- A stakeholder can see confidence and risk labels, but not why one factor outweighed another.

### What would make this persona trust it more

- Explanations that point more directly to the dominant cause of the current label.
- Less ambiguity between “heuristic interpretation” and “objective measurement.”
- Clearer cues that these are advisory planning judgments, not predictive guarantees.

### Persona verdict

- **Readable, but only moderately credible without prior explanation.**
- The UI is understandable.
- The trust gap is in evidential strength, not in wording alone.

---

## Top 5 signal failures across all personas

1. **Systemic overcommitment can be understated**
   - board-relative normalization can make a globally crowded plan look normal.
2. **Far-future confidence is still somewhat optimistic**
   - stable distant sprints top out at `Medium`, but much of the horizon can remain `High` too long.
3. **Parallelism realism is only partially captured**
   - track count signals structure, but not whether the parallel plan is operationally believable.
4. **Explanations do not always show the dominant evidence**
   - chips are capped and some factors are mutually suppressed in the explanation layer.
5. **Latest-impact summaries can underrepresent distributed changes**
   - they prioritize the single strongest sprint delta rather than the full shape of the change.

## Top 3 strengths of the current system

1. **Risk and confidence are clearly separated**
   - users are not forced into one blended score.
2. **Confidence decay is gradual**
   - the system avoids the earlier binary feel.
3. **The UI is readable at a glance**
   - labels, chips, tooltips, and heat styling make the signal model accessible.

## 3 disagreements between personas

1. **Execution-focused PO vs strategic planner**
   - the PO values that small future changes do not overreact;
   - the strategic planner sees the same behavior as too optimistic for long-range plans.
2. **Execution-focused PO vs skeptical stakeholder**
   - the PO accepts directional heuristics if red sprints look dangerous;
   - the skeptical stakeholder doubts signals that do not show concrete evidence.
3. **Strategic planner vs skeptical stakeholder**
   - the strategic planner appreciates smooth decay;
   - the skeptical stakeholder sees smoothness without transparent evidence as possible guesswork.

## 2 high-risk misinterpretations

1. **“Green or yellow means the whole plan is safe.”**
   - incorrect because the signal is sprint-relative and board-relative, not a whole-plan health verdict.
2. **“High confidence means we are likely to deliver.”**
   - incorrect because confidence reflects planning stability, not delivery probability.

## Overall verdict

**The signals are conditionally trustworthy, not fully trustworthy.**

They are useful as directional planning heuristics and are much more credible than a raw colored metric layer. They align reasonably well with concentrated sprint strain and now avoid abrupt confidence swings. However, they still under-express global overcommitment, remain somewhat optimistic in the far future, and do not always provide evidence strong enough for skeptical readers to trust the labels without context.

---

## Per-persona answer summary

| Persona | Trusts | Doubts | Feels incorrect/misleading | Would trust more if |
|---|---|---|---|---|
| Execution-focused PO | Concentrated sprint strain | systemic overcommitment normalization | globally dense boards can look too normal | normalization masked overload less often |
| Strategic planner | gradual decay, stable near-term confidence | far future remains too calm | long horizon can stay high-confidence too long | long-range provisionality was clearer |
| Skeptical stakeholder | readable separation and plain language | credibility of hidden heuristics | explanations feel broad and post-hoc | dominant causes were more explicit |

## Final answer to the expected result

- **Does the system align with real-world intuition?**
  - Partially. Stronger on concentrated sprint pressure than on broad planning realism.
- **Where is calibration still needed?**
  - Mainly in chronic overload interpretation, long-range confidence tone, and explanation credibility.
- **Will users trust it in practice?**
  - Some will trust it as a planning aid; skeptical users will not fully trust it as evidence on its own.
