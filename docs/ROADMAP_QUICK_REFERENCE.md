# MacroChef Roadmap: Quick Reference Guide

**Document:** `docs/ROADMAP.md` (1600+ lines, comprehensive guide)
**Last Updated:** December 2025

---

## At a Glance

### Timeline & Priorities

```
Q1-Q2 2026        Q2-Q3 2026         Q3-Q4 2026           Q4 2026+
├─ Trainer P0     ├─ AI Features P1   ├─ Moderation P2      ├─ Extensions P3
│  (14 weeks)     │  (18 weeks)       │  (7 weeks)          │  (On demand)
│                 │                   │                     │
├─ Meal plans     ├─ Recommendations  ├─ Image moderation   ├─ Barcode scan
├─ Dashboard      ├─ AI meal plans    ├─ Text moderation    ├─ Fitness sync
├─ Goals          ├─ Nutrition        ├─ Appeals system     ├─ Recipe import
├─ Bulk ops       │  insights         │                     ├─ Social features
└─ Permissions    └─ Macro optimizer  └─────────────────────┴─ Analytics
                                                               ├─ Mobile app
```

### Priority Definitions

| Priority | Timeline | Business Impact | Revenue | Users |
|----------|----------|-----------------|---------|-------|
| **P0** | Now | Revenue generation | HIGH | Trainers |
| **P1** | Next | Engagement & retention | MEDIUM | Everyone |
| **P2** | Soon | Risk mitigation | LOW | Everyone |
| **P3** | Later | Market expansion | TBD | Everyone |

---

## Quick Navigation

### P0: Trainer Features (14 weeks, ~$84k)

**Why:** Trainers are the primary revenue drivers. Complete platform enables premium tiers and B2B partnerships.

| Feature | Duration | Complexity | Key Endpoint | Page |
|---------|----------|-----------|--|-----|
| 1.1 Meal Plan Assignment | 4w | Medium | `POST /api/Trainers/bulk-assign-meal-plans` | p.12 |
| 1.2 Progress Dashboard | 3w | Medium | `GET /api/Trainers/dashboard` | p.18 |
| 1.3 Goal Setting | 2w | Small-Med | `POST /api/Trainers/clients/{id}/goals` | p.27 |
| 1.4 Bulk Operations | 3w | Medium | `POST /api/Trainers/bulk-workout-assign` | p.32 |
| 1.5 Enhanced Permissions | 2w | Medium | Permission matrix RBAC | p.37 |

**Start:** Jan 6, 2026 | **End:** Feb 17, 2026

---

### P1: AI Features (18 weeks, ~$108k + ~$6k/year API costs)

**Why:** Differentiation via smart recommendations. Improves engagement, reduces trainer workload.

| Feature | Duration | Complexity | Tech | API Cost | Page |
|---------|----------|-----------|------|----------|-----|
| 2.1 Meal Recommendations | 5w | Medium | GPT-4 | ~$0.03-0.06 ea. | p.48 |
| 2.2 AI Meal Plans | 6w | Large | GPT-4 + validation | ~$0.10 ea. | p.63 |
| 2.3 Nutrition Insights | 4w | Medium | Python analytics | Free | p.77 |
| 2.4 Macro Optimization | 3w | Medium | GPT-4 | ~$0.05 ea. | p.90 |

**Start:** Feb 17, 2026 | **End:** May 26, 2026

**Cost Estimate:**
- 1000 DAU × $0.05/rec × 3 recs/week = ~$750/month API
- With caching optimization: ~$500/month

---

### P2: Content Moderation (7 weeks, ~$42k + ~$600/year API costs)

**Why:** Risk mitigation (liability, brand protection), compliance (COPPA, GDPR).

| Feature | Duration | Complexity | Tech | API Cost | Page |
|---------|----------|-----------|------|----------|-----|
| 3.1 Image Moderation | 3w | Medium | Azure Content Safety | ~$1/1000 images | p.102 |
| 3.2 Text Moderation | 2w | Small | OpenAI Moderation | Free | p.120 |
| 3.3 Appeals System | 2w | Small | Custom UI + DB | Free | p.127 |

**Start:** Apr 28, 2026 | **End:** Jun 9, 2026

**Cost Estimate:**
- 10k images/month × $0.001 = ~$10/month

---

### P3: Extensions (Variable, Low Priority)

**Not scheduled.** Implement as capacity allows. Estimated costs TBD.

| Feature | Est. Weeks | Est. Cost | Page | Rationale |
|---------|-----------|-----------|-----|-----------|
| Barcode Scanning | 2-3w | $12-18k | p.135 | Low effort, high engagement |
| Fitness Tracker Sync | 5-6w | $30-36k | p.141 | High engagement, multi-platform |
| Recipe Import | 3-4w | $18-24k | p.151 | Medium effort, user retention |
| Social Features | 8-10w | $48-60k | p.160 | High effort, engagement differentiator |
| Advanced Analytics | 6-8w | $36-48k | p.166 | Trainer value add |
| Mobile App | 12-16w | $72-96k | p.173 | Market expansion |

---

## Key Dependencies & Blockers

### P0 Completion Needed For:
- All P1 features (AI needs stable foundation)
- Bulk operations work better with P0 infrastructure
- UI improvements from trainer feedback

### P1 Completion Needed For:
- P3 Advanced Analytics (uses AI insights)
- Social features (sharing AI-generated plans)

### External Dependencies:
- **OpenAI API account** (GPT-4 access, Moderation API)
- **Azure Content Safety** subscription
- **Fitness tracker APIs** (Fitbit, Apple Health, Google Fit)
- **Food database** enhancement (nutrients, barcodes)

---

## Success Metrics by Phase

### P0 Phase Success
- 90% of trainers using bulk operations
- 100% of trainers creating meal plans
- Dashboard adoption: 80%+
- No regressions in existing trainer features

### P1 Phase Success
- 40% of users use meal recommendations weekly
- 20% of users generate AI meal plans
- AI recommendation cost < $0.05/user/week average
- User retention +15% over baseline

### P2 Phase Success
- 100% of images scanned
- False positive rate < 5%
- Appeals resolved within 24h
- Zero moderation-related support tickets

---

## Budget Overview

### Development Costs
- **P0 (Trainer completion):** $84,000 (1 engineer, 14 weeks)
- **P1 (AI features):** $108,000 (1 engineer, 18 weeks)
- **P2 (Moderation):** $42,000 (0.5 engineer, 7 weeks)
- **Total P0-P2:** ~$234,000 over 9 months

### Annual Operational Costs (P0-P2)
| Service | Cost |
|---------|------|
| OpenAI API | $6,000 |
| Azure Content Safety | $600 |
| Azure App Insights | $600 |
| AWS S3 (images) | $1,200 |
| Postgres (larger) | $2,400 |
| Redis (larger) | $600 |
| **Total** | **$11,400/year** |

---

## Risk Summary

### High Risk
- **OpenAI API rate limits** (Mitigation: queue system, fallback to deterministic)
- **False positives in moderation** (Mitigation: appeals system, tuning)

### Medium Risk
- **Performance at scale** (Mitigation: snapshots, data warehouse post-P1)
- **AI costs exceeding budget** (Mitigation: monitoring, usage-based gating)

### Low Risk (Well-Mitigated)
- Regulatory compliance (legal review planned)
- Health claim liability (disclaimers, informational-only framing)

---

## Feature Flags for Safe Rollout

All new features behind feature flags:

```csharp
public class FeatureFlags
{
    public bool MealRecommendationsEnabled { get; set; }
    public double MealRecommendationBeta { get; set; } // % of users (0-1)
    public bool AiMealPlansEnabled { get; set; }
    public bool ContentModerationEnabled { get; set; }
    public bool ImageModerationEnabled { get; set; }
    // ... etc
}
```

**Rollout Phase:**
1. **Alpha (Internal):** Feature team only (Week 1)
2. **Beta (Limited):** 10% of users (Weeks 2-3)
3. **Wider Beta:** 50% of users (Weeks 4-6, if metrics good)
4. **General Availability:** 100% (Week 7+)

**Instant rollback:** Disable flag in < 1 second if issues arise.

---

## Key Takeaways for Developers

### 1. P0 is Critical Path
Everything else blocks on trainer features. Start here.

### 2. AI Features Have High Value but Non-Zero Cost
Budget for OpenAI API. Implement caching aggressively. Monitor spend.

### 3. Testing is Non-Negotiable
- Mock OpenAI responses in tests
- Build comprehensive moderation test sets
- Load test meal plan generation at scale

### 4. Start with Feature Flags
Every new feature ships disabled. Enable gradually.

### 5. Document as You Go
Update `docs/ARCHITECTURE.md` with AI integration patterns, moderation flow, etc.

---

## Implementation Checklist

### Before Starting P0
- [ ] Review full ROADMAP.md (especially technical requirements)
- [ ] Set up feature flags infrastructure
- [ ] Plan database migrations for new entities
- [ ] Set up monitoring/alerts for API endpoints

### Before Starting P1
- [ ] OpenAI API account with GPT-4 access
- [ ] Cost tracking setup (alerts if spend > $X/day)
- [ ] Fallback strategy documented and tested
- [ ] Food database enriched with nutrients

### Before Starting P2
- [ ] Azure Content Safety subscription
- [ ] Moderation test dataset prepared
- [ ] Admin review UI mocked out
- [ ] Legal review of moderation policies

---

## Questions? Refer to:

| Question | Section |
|----------|---------|
| How much will this cost? | "Budget Estimate" (full doc, p.155) |
| What's the technical architecture for X? | Feature section (e.g., 2.1 for meal recs) |
| How do we roll out safely? | "Rollout Strategy" (p.161) |
| What could go wrong? | "Risk Assessment" (p.149) |
| What are the success criteria? | "Success Metrics" (p.138) |
| When should we add feature X? | "Implementation Timeline" (p.125) |

---

## Contact & Feedback

This roadmap is a living document. Quarterly review recommended (next: Q2 2026) to adjust based on:
- Actual team capacity
- User adoption metrics
- Market changes
- Technology updates

**Last Updated:** December 2025
**Next Review:** March 2026
