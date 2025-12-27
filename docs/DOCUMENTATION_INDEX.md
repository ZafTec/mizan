# MacroChef Documentation Index

**Project:** MacroChef (Mizan) - Full-stack meal planning and nutrition tracking application
**Created:** 2025-12-27
**Version:** 1.0

---

## Overview

This index provides a comprehensive guide to all documentation in the MacroChef project. Documentation is organized by purpose and audience. All files are located in the `docs/` directory unless otherwise noted.

---

## Quick Navigation

### For New Developers
Start here and read in order:
1. **CLAUDE.md** (root) - Project-specific guidelines and quick reference
2. **DEVELOPER_ONBOARDING.md** - Local setup, first build, common tasks (30 min read)
3. **ARCHITECTURE.md** - System design and why decisions were made (20 min read)
4. **TROUBLESHOOTING.md** - Solutions to common problems (reference)

### For Specific Tasks
- **Adding API endpoint** → API_REFERENCE.md
- **Deploying to production** → DEPLOYMENT_GUIDE.md
- **Writing tests** → TESTING_GUIDE.md
- **Real-time features** → SIGNALR_IMPLEMENTATION.md
- **Something is broken** → TROUBLESHOOTING.md

### For Understanding the System
- **Architecture and design** → ARCHITECTURE.md
- **Security model** → SECURITY.md
- **Feature history** → CHANGELOG.md

---

## Documentation Structure

### Core Documentation (Must Read)

#### 1. CLAUDE.md (root directory)
**Purpose:** Project-specific guidance for developers and LLMs
**Size:** ~3,000 words
**Time to read:** 5-10 minutes
**When to read:**
- Before starting any work on the project
- When implementing features
- When you need quick reference on commands

**Contains:**
- Communication style and calibration phrases
- Tech stack overview
- Essential commands for backend, frontend, Docker
- Architecture principles (schema separation, Clean Architecture)
- Code generation guidelines
- Common workflows
- File location reference
- Critical reminders

#### 2. DEVELOPER_ONBOARDING.md
**Purpose:** Get new developers productive in 30 minutes
**Size:** ~5,000 words
**Time to read:** 15-20 minutes
**When to read:**
- First time setting up development environment
- Onboarding to the project
- Learning project structure and conventions

**Contains:**
- Welcome message and project overview
- Environment setup for Windows/Mac/Linux
- First build with Docker Compose
- Complete directory structure for frontend and backend
- Daily development workflow
- Git workflow and commit conventions
- Common development tasks with code examples
- Code style and naming conventions
- How to get help

#### 3. ARCHITECTURE.md
**Purpose:** Comprehensive system design documentation
**Size:** ~7,000 words
**Time to read:** 20-30 minutes
**When to read:**
- Understanding overall system design
- Learning about schema separation (intentional design)
- Understanding data flow and communication patterns
- Learning about authentication and authorization

**Contains:**
- Architecture principles and philosophy
- Schema boundaries (Drizzle vs EF Core)
- Data flow diagrams (request, authentication, type safety)
- API routing and proxy configuration
- Security architecture (BFF pattern)
- Authorization and three-tier roles
- Trainer features architecture
- Real-time communication (SignalR)
- Caching strategy
- Backend Clean Architecture layers
- Frontend structure
- Code generation workflow
- Testing strategy
- Deployment overview

#### 4. TROUBLESHOOTING.md
**Purpose:** Solve 90% of common issues quickly
**Size:** ~8,000 words
**Time to read:** Reference (search for your issue)
**When to read:**
- Something is broken
- Services won't start
- Type errors
- API errors
- Tests failing

**Contains:**
- Quick checklist (restart, logs, health check, cache)
- Docker & Infrastructure issues
- Backend (ASP.NET Core) issues
- Frontend (Next.js) issues
- Database & Cache issues
- Authentication & Security issues
- Real-time features (SignalR) issues
- Deployment issues
- Performance issues
- Testing issues
- Emergency procedures (full reset, restore from backup)

---

### API and Integration Documentation

#### 5. API_REFERENCE.md
**Purpose:** Complete reference for all API endpoints
**Size:** ~6,000 words
**Time to read:** Reference (search for endpoint)
**When to read:**
- Adding new API endpoints
- Calling backend APIs from frontend
- Understanding request/response formats
- Error codes and handling

**Contains:**
- Authentication endpoints (sign up, sign in, sign out)
- User profile endpoints
- Food and ingredient endpoints
- Recipe endpoints (CRUD, search)
- Meal plan endpoints
- Shopping list endpoints
- Workout and exercise endpoints
- Body measurements endpoints
- Household endpoints
- Trainer endpoints (client management, data access)
- Chat endpoints
- Admin endpoints (user management)
- Error codes and HTTP status meanings

---

### Feature Documentation

#### 6. SIGNALR_IMPLEMENTATION.md
**Purpose:** Real-time chat and notification implementation guide
**Size:** ~3,000 words
**Time to read:** 15-20 minutes
**When to read:**
- Working on chat features
- Adding real-time notifications
- Debugging WebSocket issues
- Understanding connection management

**Contains:**
- SignalR architecture and hubs
- Frontend chat service implementation
- Backend hub configuration
- Authentication and authorization
- Message persistence
- Connection management
- Error handling
- Testing real-time features
- Deployment considerations (Redis backplane)

#### 6a. IMPLEMENTATION_2025_12_27.md
**Purpose:** December 2025 feature implementations (recipe visibility, trainer discovery)
**Size:** ~12,000 words
**Time to read:** 25-30 minutes
**When to read:**
- Understanding recipe visibility feature
- Working on trainer discovery/request features
- Integrating trainer-client relationship queries
- Extending trainer features (permissions, acceptance flow)
- Writing tests for new features
- Understanding new backend queries and DTOs

**Contains:**
- Recipe visibility feature (private/household/public)
- Trainer discovery page (browse available trainers)
- Trainer requests page (manage pending requests)
- My trainer page (view active relationship)
- Backend query handlers (GetAvailableTrainersQuery, GetMyTrainerQuery, GetMyTrainerRequestsQuery)
- Data relationships and access patterns
- Performance considerations and caching opportunities
- Testing strategies for all features
- Known issues and future enhancements
- File changes summary and deployment checklist

---

### Deployment and Operations

#### 7. DEPLOYMENT_GUIDE.md
**Purpose:** Production deployment and operations procedures
**Size:** ~8,000 words
**Time to read:** 20-30 minutes (before first production deployment)
**When to read:**
- Deploying to production
- Setting up SSL/TLS
- Configuring backups
- Monitoring and health checks
- Scaling the application
- Database maintenance

**Contains:**
- Prerequisites (Docker, domain, etc.)
- Environment configuration (frontend, backend, Docker Compose)
- Docker production deployment setup
- Database setup and migrations
- SSL/TLS with Let's Encrypt
- Backup and restore procedures
- Health checks and monitoring
- Scaling (horizontal, database replicas)
- Troubleshooting deployment issues
- Security checklist
- Maintenance procedures
- Performance optimization

---

### Testing and Quality

#### 8. TESTING_GUIDE.md
**Purpose:** Testing strategy, tools, and examples
**Size:** ~10,000 words
**Time to read:** 20-30 minutes (before writing tests)
**When to read:**
- Writing tests for new features
- Understanding test philosophy and patterns
- Setting up test fixtures
- Running tests in CI/CD

**Contains:**
- Testing pyramid approach (E2E > Integration > Unit)
- Testing philosophy and rules
- Backend testing (xUnit, FluentAssertions, Testcontainers)
- Frontend testing (Vitest, Testing Library, Playwright)
- Unit test examples (commands, queries, handlers)
- Integration test examples
- E2E test examples
- Component test examples
- Hook test examples
- Authorization testing patterns
- Role-based testing matrix
- Test data management and fixtures
- CI/CD integration examples
- Coverage goals
- Best practices and troubleshooting

---

### Security and Compliance

#### 9. SECURITY.md
**Purpose:** Security policies, guidelines, and vulnerability reporting
**Size:** ~2,000 words
**Time to read:** 10-15 minutes
**When to read:**
- Before deploying to production
- Reporting security issues
- Understanding security policies
- Security best practices

**Contains:**
- Security policy overview
- Vulnerability reporting procedures
- Responsible disclosure guidelines
- Known limitations and mitigations
- Security features (JWT, CSRF, CORS, XSS protection)
- Data handling and privacy
- Compliance considerations

---

### Version Control and Changes

#### 10. CHANGELOG.md
**Purpose:** Version history and release notes
**Size:** ~4,000 words
**Time to read:** Reference (scan recent changes)
**When to read:**
- Understanding what changed in recent versions
- Finding when a feature was added
- Checking breaking changes

**Contains:**
- Unreleased changes
- Versioned release notes (1.2.0, 1.1.0, etc.)
- Added features
- Fixed bugs
- Changed behavior
- Deprecated features
- Known issues

---

### Strategic Planning and Roadmap

#### 11. ROADMAP.md
**Purpose:** Comprehensive strategic roadmap for advanced features (18-24 months)
**Size:** ~20,000 words (1600+ lines)
**Time to read:** 45-60 minutes (executive summary: 5 min)
**When to read:**
- Planning next development phases
- Understanding product strategy and priorities
- Evaluating feature business value
- Capacity planning and budgeting
- Setting team quarterly objectives (OKRs)

**Contains:**
- Executive summary with strategic priorities
- Priority legend (P0 Critical → P3 Lower)
- **P0: Trainer Feature Completion** (14 weeks, $84k)
  - Meal plan assignment and management
  - Progress monitoring dashboard
  - Client goal setting and tracking
  - Bulk operations and workflow automation
  - Enhanced permission and access control
- **P1: AI-Powered Insights** (18 weeks, $108k + $6k/year API)
  - Personalized meal recommendations (GPT-4)
  - AI-generated meal plans
  - Nutrition insights and trend analysis
  - Macronutrient optimization recommendations
- **P2: Content Moderation** (7 weeks, $42k + $600/year API)
  - Image content moderation (Azure Content Safety)
  - Text content moderation (OpenAI)
  - Automated review queue and appeals system
- **P3: Advanced Features** (Variable timeline)
  - Barcode scanning for food logging
  - Fitness tracker integration (Fitbit, Apple Health, Google Fit)
  - Recipe import from websites
  - Social features and community
  - Advanced analytics and reporting
  - Mobile app development (React Native/Flutter)
- Implementation timeline and dependencies
- Technical requirements with code examples
- Success metrics and KPIs
- Risk assessment and mitigation strategies
- Budget estimates (development, API costs, infrastructure)
- Feature flags and safe rollout strategy
- 18-24 month phased approach

#### 12. ROADMAP_QUICK_REFERENCE.md
**Purpose:** Quick-lookup guide to roadmap priorities and timelines
**Size:** ~3,000 words
**Time to read:** 5-10 minutes
**When to read:**
- Quickly checking feature priorities
- Finding which phase a feature is in
- Budget estimates and dependencies
- Risk summary
- Success metrics

**Contains:**
- Timeline visualization (Q1-Q4 2026+)
- Quick navigation table for each priority (P0-P3)
- Priority definitions
- Key dependencies and blockers
- Success metrics by phase
- Budget overview (dev costs, annual operational costs)
- Risk summary (high/medium/low)
- Feature flags for safe rollout
- Implementation checklist
- Quick reference for common questions

---

## Documentation by Audience

### New Developer (First Day)

| Read | Time | Purpose |
|------|------|---------|
| CLAUDE.md | 5 min | Get oriented to project |
| DEVELOPER_ONBOARDING.md | 15 min | Set up environment and build |
| ARCHITECTURE.md | 20 min | Understand system design |

**Result:** Ready to write your first feature!

### Experienced Developer (Adding Feature)

| Read | Time | Purpose |
|------|------|---------|
| CLAUDE.md (quick ref) | 2 min | Refresh on commands |
| API_REFERENCE.md | 10 min | Understand API contract |
| TESTING_GUIDE.md | 10 min | Test patterns |
| Specific docs | As needed | Feature-specific |

**Result:** Implement feature with tests and documentation.

### DevOps/Operations

| Read | Time | Purpose |
|------|------|---------|
| ARCHITECTURE.md | 20 min | System overview |
| DEPLOYMENT_GUIDE.md | 30 min | Production setup |
| TROUBLESHOOTING.md | Reference | Debug issues |
| SECURITY.md | 15 min | Security posture |

**Result:** Deploy and maintain production environment.

### QA/Tester

| Read | Time | Purpose |
|------|------|---------|
| TESTING_GUIDE.md | 20 min | Test patterns |
| API_REFERENCE.md | 15 min | Endpoint reference |
| TROUBLESHOOTING.md | Reference | Diagnose issues |
| CHANGELOG.md | Reference | What's new |

**Result:** Write comprehensive tests and test plans.

---

## Documentation Maintenance

### When to Update

- **After feature release** → Update CHANGELOG.md
- **After changing architecture** → Update ARCHITECTURE.md
- **After adding endpoint** → Update API_REFERENCE.md
- **After discovering common issue** → Add to TROUBLESHOOTING.md
- **After changing deployment** → Update DEPLOYMENT_GUIDE.md
- **After changing test patterns** → Update TESTING_GUIDE.md

### Quality Standards

All documentation should:
- ✅ Be accurate and match actual implementation
- ✅ Include code examples where helpful
- ✅ Use clear section headers for scanability
- ✅ Provide "when to read" guidance
- ✅ Include troubleshooting for complex topics
- ✅ Reference other docs appropriately
- ✅ Be kept current with code changes

### Git Workflow for Docs

```bash
# Create feature branch
git checkout -b docs/update-deployment-guide

# Make changes
vim docs/DEPLOYMENT_GUIDE.md

# Commit (with feature)
git add docs/
git commit -m "docs: add scaling section to deployment guide"

# Push and create PR
git push origin docs/update-deployment-guide
```

---

## Documentation Statistics

| Document | Lines | Words | Purpose |
|----------|-------|-------|---------|
| ARCHITECTURE.md | ~660 | 7,000+ | System design |
| API_REFERENCE.md | ~500 | 6,000+ | Endpoint reference |
| DEPLOYMENT_GUIDE.md | ~730 | 8,000+ | Production ops |
| TESTING_GUIDE.md | ~710 | 10,000+ | Test patterns |
| DEVELOPER_ONBOARDING.md | ~530 | 5,000+ | Onboarding guide |
| TROUBLESHOOTING.md | ~850 | 8,000+ | Issue solving |
| SIGNALR_IMPLEMENTATION.md | ~350 | 3,000+ | Real-time features |
| SECURITY.md | ~150 | 2,000+ | Security policy |
| CHANGELOG.md | ~200 | 4,000+ | Version history |
| ROADMAP.md | ~1,661 | 20,000+ | Strategic roadmap |
| ROADMAP_QUICK_REFERENCE.md | ~330 | 3,000+ | Quick reference |
| CLAUDE.md (root) | ~700 | 3,000+ | Dev guidelines |
| **Total** | **~7,870** | **~79,000+** | Complete documentation |

---

## Search and Find

### By Topic

**Authentication**
- CLAUDE.md → Authentication Flow
- ARCHITECTURE.md → Security Architecture
- API_REFERENCE.md → Authentication endpoints
- SECURITY.md → Security features

**Database**
- ARCHITECTURE.md → Schema Boundaries
- DEPLOYMENT_GUIDE.md → Database Setup
- TROUBLESHOOTING.md → Database & Cache

**Testing**
- TESTING_GUIDE.md → All testing topics
- CLAUDE.md → Testing Philosophy
- DEVELOPER_ONBOARDING.md → Running Tests

**Deployment**
- DEPLOYMENT_GUIDE.md → All deployment topics
- TROUBLESHOOTING.md → Deployment Issues
- ARCHITECTURE.md → Deployment overview

**Troubleshooting**
- TROUBLESHOOTING.md → All common issues
- DEPLOYMENT_GUIDE.md → Troubleshooting section
- SIGNALR_IMPLEMENTATION.md → SignalR issues

### By Error Message

- "Cannot find module" → TROUBLESHOOTING.md (Frontend issues)
- "Connection refused" → TROUBLESHOOTING.md (Docker & Infrastructure)
- "JWT validation failed" → TROUBLESHOOTING.md (Authentication & Security)
- "Slow responses" → TROUBLESHOOTING.md (Performance Issues)
- "Tests hang" → TROUBLESHOOTING.md (Testing Issues)

---

## Quick Commands Reference

```bash
# Start development
docker-compose up -d
docker-compose logs -f

# Frontend development
cd frontend
bun install
bun run dev
bun run test
bun run test:e2e
bun run codegen  # After backend changes

# Backend development
cd backend
dotnet build
dotnet run --project Mizan.Api
docker-compose --profile test up test
dotnet ef migrations add MyMigration --project Mizan.Infrastructure --startup-project Mizan.Api

# Debugging
docker logs mizan-frontend
docker logs mizan-backend
docker exec -it mizan-postgres psql -U mizan -d mizan

# Deployment
docker-compose -f docker-compose.prod.yml up -d
docker-compose -f docker-compose.prod.yml logs -f
```

See CLAUDE.md for complete command reference.

---

## Contributing to Documentation

**We welcome documentation improvements!**

To contribute:
1. Create a branch: `git checkout -b docs/your-topic`
2. Make changes following existing style
3. Verify accuracy against code
4. Commit: `git commit -m "docs: what changed"`
5. Submit PR

**Style guide:**
- Use clear section headers
- Include code examples for complex topics
- Provide "when to read" guidance
- Keep lines under 100 characters where possible
- Use tables for structured information
- Link to related documentation

---

## Support and Questions

### Getting Help

1. **Check CLAUDE.md** - Quick reference (5 min)
2. **Check TROUBLESHOOTING.md** - Common issues (10 min)
3. **Check specific docs** - Topic reference (15 min)
4. **Review code examples** - See working implementations
5. **Ask in discussions** - If still stuck

### Reporting Issues

- **Bug:** Create GitHub issue with details
- **Security:** See SECURITY.md for responsible disclosure
- **Documentation:** Create issue or submit PR

---

**Last Updated:** 2025-12-27
**Documentation Version:** 1.0
**Project Version:** 1.2.0

For the latest updates, check CHANGELOG.md.
