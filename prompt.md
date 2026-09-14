# Production Readiness Review and Azure Deployment Preparation

## Role

Act as a senior cloud architect and backend/platform engineer responsible for preparing this project for production deployment on Azure.

Your goal is not to deploy immediately. First, perform a complete production readiness audit of the existing codebase, infrastructure, configuration, real-time communication layer, and deployment setup.

The target environment:

- Azure deployment
- Production workload
- Approximately 200 concurrent students/users
- Real-time functionality is used in the application (WebSockets / SignalR or equivalent)
- The application must remain reliable under concurrent usage

---

# Objective

Review the entire project and identify anything that could cause problems in production.

Analyze:

- Application configuration
- Environment variables
- Secrets management
- Startup process
- Deployment configuration
- Database configuration
- Real-time communication scalability
- Performance bottlenecks
- Resource usage
- Logging and observability
- Security concerns
- Azure deployment readiness

Do not make random changes immediately.

First understand the current architecture, then produce a detailed production improvement plan.

---

# Review Areas

## 1. Environment Configuration

Review:

- `.env` files
- environment variable usage
- appsettings files
- production configuration
- missing environment variables
- hardcoded values
- secrets committed to the repository
- development-only settings leaking into production

Verify:

- Secrets are not stored in source code
- Production values can be injected safely through Azure environment configuration
- Required variables are documented
- Missing variables fail safely during startup

---

## 2. Application Startup and Runtime

Review:

- Application startup flow
- Dependency injection configuration
- Service registration
- Middleware ordering
- Health checks
- Graceful shutdown handling
- Startup failures
- Background services

Check:

- Can the application start reliably in Azure?
- Are migrations/seeding handled safely?
- Are there blocking operations during startup?
- Are there unnecessary dependencies that increase startup time?

---

## 3. Performance Review

Analyze the application for:

### Backend performance

Look for:

- Slow database queries
- N+1 queries
- Missing indexes
- Inefficient EF Core usage
- Unnecessary database round trips
- Large object loading
- Blocking async calls
- Memory leaks
- Excessive allocations

Review:

- Query patterns
- Transactions
- Connection pooling
- Caching opportunities

---

## 4. Real-Time System Review

The system supports real-time communication.

Review:

- SignalR/WebSocket architecture
- Connection management
- Reconnection handling
- Broadcasting strategy
- Memory usage per connection
- Concurrent connection handling
- Message ordering
- Failure scenarios

Validate against:

- 200 concurrent connected users
- Simultaneous actions from many users
- Network interruptions
- Server restart scenarios

Identify:

- Scaling limitations
- Possible bottlenecks
- Required Azure configuration
- Whether Redis backplane or another mechanism is needed

Do not add complexity unless measurements justify it.

---

## 5. Database Production Review

Review:

- Database configuration
- Connection strings
- Connection pooling
- EF Core migrations
- Indexes
- Constraints
- Transactions
- Query performance

Check:

- Production database safety
- Backup requirements
- Migration strategy
- Handling concurrent writes

---

## 6. Docker and Deployment Review

Review:

- Dockerfiles
- docker-compose files
- production compose configuration
- container health checks
- resource limits
- networking
- volumes
- startup dependencies

Check:

- Containers are production-ready
- No unnecessary exposed ports
- Proper restart policies
- Proper logging configuration
- Resource limits are defined

---

## 7. Azure Deployment Readiness

Review the project for Azure deployment.

Analyze:

- Recommended Azure service
- Required resources
- Environment configuration
- Networking
- Scaling approach
- Cost considerations
- Monitoring requirements

Consider:

- Azure App Service
- Azure Container Apps
- Azure VM
- Azure Database options

Recommend the most suitable approach based on the current architecture.

---

## 8. Security Review

Check:

- Authentication
- Authorization
- JWT configuration
- Token expiration
- CORS
- CSRF risks
- Rate limiting
- Input validation
- File uploads
- Sensitive data exposure
- Logging sensitive information

Verify:

- No secrets appear in logs
- No production credentials exist in code
- Error messages do not leak internal information

---

## 9. Observability Review

Review:

- Logging
- Metrics
- Distributed tracing
- Health checks
- Monitoring dashboards

Check whether the project supports:

- Detecting failures
- Measuring latency
- Finding database bottlenecks
- Monitoring real-time connections
- Debugging production issues

Recommend improvements using:

- OpenTelemetry
- Prometheus
- Grafana
- Azure Monitor (if appropriate)

---

# Performance Target

The system should be prepared for:

- 200 concurrent users/students
- Real-time connections
- Burst traffic during important actions

Evaluate:

- CPU requirements
- Memory requirements
- Database capacity
- Network usage
- Real-time connection limits

Identify:

- Current bottlenecks
- Future bottlenecks
- Risk areas

---

# Output Requirement

Create a file:

```

production.md

````

This file should contain the complete production readiness report.

Structure:

```md
# Production Readiness Report

## Executive Summary

Current production readiness:
- Ready
- Partially ready
- Not ready

Main risks:

---

# Critical Issues

Issues that must be fixed before deployment.

For each issue:

## Problem

Explain the issue.

## Impact

Explain what can happen in production.

## Recommended Fix

Explain the solution.

## Priority

Critical / High / Medium / Low

---

# Performance Review

## Current Architecture

Explain current behavior.

## Bottlenecks

List possible bottlenecks.

## Recommendations

Explain improvements.

---

# Real-Time System Review

## Current Design

Explain how real-time communication works.

## Risks

Explain scalability concerns.

## Recommendations

---

# Azure Deployment Recommendations

Recommended architecture:

Explain:

- Services
- Networking
- Environment configuration
- Scaling strategy

---

# Security Improvements

List required security changes.

---

# Observability Improvements

List monitoring and tracing improvements.

---

# Deployment Checklist

Before deployment:

- [ ] Environment variables configured
- [ ] Secrets moved outside code
- [ ] Database production configuration ready
- [ ] Health checks added
- [ ] Logging configured
- [ ] Monitoring enabled
- [ ] Load testing completed

---

# Implementation Priority

Provide a prioritized roadmap:

Phase 1:
Critical fixes before production

Phase 2:
Performance improvements

Phase 3:
Operational improvements

Phase 4:
Future scalability improvements
````

---

# Important Rules

* Do not modify the code yet.
* Only review and document findings.
* Do not assume something is correct without checking the implementation.
* Base every recommendation on the actual project.
* Mention when something is already implemented correctly.
* Avoid unnecessary architecture changes.
* Prefer simple production solutions before introducing complex infrastructure.

For every recommendation explain:

1. What is wrong?
2. Why does it matter?
3. How should it be fixed?
4. What is the tradeoff?

---

After creating `production.md`, summarize the top 10 production risks and the recommended order to fix them.

```

This version is designed for an agent that will **inspect first, avoid unnecessary refactoring, and produce an actionable production audit** instead of blindly changing files.
```
