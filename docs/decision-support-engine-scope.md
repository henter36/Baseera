# Baseera Decision Support Engine — Scope and Architecture

## 1. Purpose

This document defines the future scope of Baseera's Decision Support Engine (DSE). The engine is intended to support prison, regional, and headquarters decision makers by converting operational data into explainable priorities, risks, scenarios, recommendations, and tracked decisions.

The engine is advisory. It must not replace authorised human decision makers or automatically execute high-impact operational, security, disciplinary, legal, or inmate-related decisions.

## 2. Research basis

The design was informed by established decision-support and trustworthy-AI practices:

- NIST AI Risk Management Framework: validity, reliability, safety, security, accountability, transparency, explainability, privacy, and fairness.
- NIST explainable-AI principles: every output should provide evidence or reasons, meaningful explanations, faithful explanations, and operate only within declared competence limits.
- AHRQ decision-support model: the right information, to the right person, in the right format, through the right channel, at the right point in the workflow.
- Public-sector algorithmic transparency practices: intended purpose, data sources, model or rule description, limitations, governance, review, and accountability must be documented.
- High-risk AI governance patterns: human oversight, logging, input-data quality, monitoring, override, and impact assessment.

These references are design guidance, not a statement that Baseera is governed by foreign law.

## 3. Comparative assessment of DSS approaches

### 3.1 Rule-based decision support

Strengths:
- Deterministic and testable.
- Easy to explain and audit.
- Suitable for policies, thresholds, escalation rules, overdue items, resource shortages, and compliance checks.

Limitations:
- Can become difficult to maintain without versioning and governance.
- Cannot discover complex patterns by itself.

Decision for Baseera:
- Core mandatory layer.

### 3.2 Scorecards and multi-criteria decision analysis

Strengths:
- Combines multiple operational dimensions.
- Supports comparison, ranking, prioritisation, and trend analysis.
- Appropriate for readiness, risk, compliance, performance, and intervention priority.

Limitations:
- Weighting can hide policy choices.
- Rankings can mislead when data quality or comparability is weak.

Decision for Baseera:
- Core layer, with documented formulas, weights, normalisation, confidence, and version history.

### 3.3 Case-based reasoning

Strengths:
- Retrieves comparable historical events and responses.
- Useful for repeated incidents, recurring observations, and lessons learned.

Limitations:
- Similarity can be subjective.
- Historical practice may contain mistakes or outdated assumptions.

Decision for Baseera:
- Recommended advisory layer; never treat past action as automatically correct.

### 3.4 Scenario and simulation support

Strengths:
- Allows decision makers to compare alternatives before action.
- Useful for capacity, resource redistribution, emergency readiness, project delay, and staffing scenarios.

Limitations:
- Results depend heavily on assumptions.
- False precision is a major risk.

Decision for Baseera:
- Recommended bounded simulation layer with explicit assumptions and sensitivity analysis.

### 3.5 Predictive analytics and machine learning

Strengths:
- Can identify deterioration patterns, demand forecasts, abnormal trends, repeated failures, and emerging risk.

Limitations:
- Requires sufficient representative historical data.
- Susceptible to drift, bias, leakage, and opaque outputs.
- Predictions can be mistaken for facts.

Decision for Baseera:
- Deferred optional layer. It may be enabled only after data-readiness, validation, monitoring, and governance gates are satisfied.

### 3.6 Generative AI

Strengths:
- Can summarise evidence, prepare briefs, explain indicators in plain Arabic, and assist exploration.

Limitations:
- May hallucinate or omit critical facts.
- Is not a reliable calculation or policy engine.

Decision for Baseera:
- Limited to evidence-grounded summarisation and explanation. It must not calculate official scores, create facts, make final decisions, or issue autonomous commands.

## 4. Target operating model

The engine shall use a hybrid architecture:

1. **Evidence layer** — approved operational facts and their provenance.
2. **Data-quality layer** — completeness, freshness, consistency, duplication, and confidence.
3. **Rules layer** — deterministic policy and escalation rules.
4. **Indicator layer** — versioned scores and metrics.
5. **Pattern layer** — trends, recurrence, correlations, and anomaly signals.
6. **Case layer** — comparable historical cases and lessons.
7. **Scenario layer** — what-if analysis and sensitivity testing.
8. **Recommendation layer** — ranked intervention options with evidence, rationale, expected effect, risks, and confidence.
9. **Human decision layer** — accept, reject, modify, defer, escalate, or request more information.
10. **Learning and assurance layer** — monitor outcomes, overrides, false alerts, drift, and rule effectiveness.

## 5. Supported decision domains

### 5.1 Facility level

- Immediate operational and security priorities.
- Readiness and resource shortages.
- Open and repeated observations.
- Incident escalation.
- Emergency-plan readiness.
- Project and corrective-action delays.
- Forms and reporting non-compliance.
- Capacity and occupancy pressures where approved data exists.

### 5.2 Region level

- Cross-facility comparison with comparability safeguards.
- Resource redistribution options.
- Facilities requiring intervention or support.
- Repeated regional patterns.
- Escalation and coordination priorities.
- Project and initiative portfolio risks.

### 5.3 Headquarters level

- National priorities and critical risks.
- Region and facility intervention portfolios.
- Strategic resource-allocation scenarios.
- National trends, systemic gaps, and recurring causes.
- Strategic programme and initiative performance.
- Decision follow-up and implementation effectiveness.

## 6. Decision product types

The engine shall produce distinct, labelled products:

- **Fact:** directly sourced operational data.
- **Indicator:** calculated result based on a documented formula.
- **Alert:** a rule or threshold has been triggered.
- **Finding:** an analytical pattern supported by evidence.
- **Forecast:** estimated future state with horizon and uncertainty.
- **Scenario:** result based on explicit user-selected assumptions.
- **Recommendation:** advisory intervention option.
- **Decision brief:** consolidated evidence for a human decision.

The UI and APIs must never present these categories as interchangeable.

## 7. Recommendation contract

Every recommendation must include:

- Scope and affected entity.
- Decision question.
- Recommendation type.
- Priority and urgency.
- Evidence references.
- Triggered rules and indicator contributions.
- Rationale.
- Proposed action and alternatives.
- Expected benefit.
- Risks and trade-offs.
- Confidence and data-quality impact.
- Validity period or expiry.
- Required decision authority.
- Whether dual approval is required.
- Status and complete decision history.

## 8. Human oversight and prohibited autonomy

The engine must support authorised users to:

- Inspect evidence and calculations.
- Request more information.
- Accept, modify, reject, defer, or escalate a recommendation.
- Record reasons for overrides.
- Stop or disable a rule, model, or recommendation family.
- Recalculate using corrected data.

The engine must not autonomously:

- Make disciplinary or legal determinations.
- Determine inmate rights, release, classification, punishment, transfer, or restriction.
- Issue operational security commands.
- Allocate or move weapons without authorised approval.
- Modify official records based solely on a prediction or generated text.
- Profile individuals for consequential decisions.

Any future extension affecting individuals requires a separate legal, ethical, privacy, security, and impact assessment.

## 9. Governance and lifecycle

Each rule, indicator, model, scenario template, and recommendation policy requires:

- Owner and approving authority.
- Intended purpose and prohibited uses.
- Version and effective dates.
- Input schema and provenance requirements.
- Test cases and validation evidence.
- Performance and quality thresholds.
- Change approval and rollback.
- Monitoring plan.
- Review and retirement date.
- Full audit trail.

Production changes must use controlled publication; draft definitions cannot affect live decisions.

## 10. Data-quality and confidence model

Every output must carry a confidence assessment derived from:

- Completeness.
- Freshness.
- Source reliability.
- Cross-source consistency.
- Coverage of required inputs.
- Model or rule applicability.

Missing data must never silently become zero. Low-confidence outputs must be suppressed, downgraded, or clearly labelled according to policy.

## 11. Explainability requirements

For every output, the engine must answer:

- What happened?
- Why was this output produced?
- Which data and rule or formula were used?
- What changed since the previous result?
- What could invalidate the result?
- What alternatives exist?
- Who is authorised to decide?

Explanations must be role-appropriate: executive, operational, analyst, auditor, and technical.

## 12. Security and privacy

- Server-side scope enforcement for Facility, Region, and Headquarters.
- Least privilege and separation of duties.
- Immutable audit events for definitions, executions, recommendations, and decisions.
- Protection against prompt injection and untrusted content if generative AI is introduced.
- No external AI provider may receive classified, security-sensitive, personal, or inmate data without explicit approval and an approved deployment architecture.
- Sensitive explanations may require redaction based on role.

## 13. Technical boundaries

The engine should be implemented as modules within the current modular monolith before considering separate services.

Recommended modules:

- DecisionSupport.Domain
- DecisionSupport.Application
- DecisionSupport.Infrastructure
- DecisionSupport.Api

Core concepts:

- DecisionDefinition
- DecisionQuestion
- EvidenceReference
- RuleDefinition / RuleVersion / RuleExecution
- IndicatorDefinition / IndicatorVersion / IndicatorResult
- DataQualityAssessment
- Alert
- Finding
- Forecast
- Scenario / ScenarioRun
- Recommendation
- DecisionRecord
- OutcomeReview
- ModelRegistry
- AssuranceReview

Execution should support synchronous evaluation for user actions and asynchronous evaluation for scheduled or event-driven workloads. Idempotency, replay, version pinning, and reproducibility are mandatory.

## 14. Phased implementation

### Phase DSE-0 — Governance and architecture

- Domain taxonomy.
- Decision catalogue.
- governance, prohibited uses, and approval workflow.
- Output contracts and audit design.

### Phase DSE-1 — Deterministic foundation

- Evidence graph.
- Data-quality and confidence service.
- Versioned rules engine.
- Alerts and explainable recommendations.
- Human decision and override workflow.

### Phase DSE-2 — Indicators and prioritisation

- Versioned scorecards.
- Multi-criteria prioritisation.
- Facility, region, and headquarters aggregation.
- Executive decision brief integration.

### Phase DSE-3 — Cases and scenarios

- Similar historical cases.
- Lessons learned.
- Scenario templates and sensitivity analysis.
- Resource and readiness what-if analysis.

### Phase DSE-4 — Advanced analytics

- Trend and anomaly detection.
- Forecasting where data readiness is proven.
- Model registry, validation, drift monitoring, and controlled rollout.

### Phase DSE-5 — Grounded AI assistance

- Evidence-grounded Arabic summaries.
- Natural-language exploration with citations to internal evidence.
- Strict separation from official calculations and decisions.

## 15. Initial success measures

- Percentage of recommendations with complete evidence and explanation.
- Time from alert to authorised decision.
- Percentage of decisions with recorded rationale.
- False-positive and dismissed-alert rates.
- Recommendation acceptance, modification, and rejection rates.
- Outcome improvement after accepted interventions.
- Data-quality improvement.
- Reproducibility rate of historical outputs.
- Zero unauthorised cross-scope disclosures.

## 16. Definition of done for the engine programme

- All production outputs are versioned, reproducible, explainable, and auditable.
- Human authority remains explicit for every consequential decision.
- The system clearly distinguishes facts, calculations, forecasts, scenarios, and recommendations.
- Data quality and uncertainty are visible.
- Rules and models have governed publication and rollback.
- Performance, security, privacy, and scope-isolation tests pass.
- Operational owners accept the engine as a decision-support tool rather than an autonomous decision maker.
