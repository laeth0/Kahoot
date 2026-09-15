# Roles and Access

## Overview

The platform supports a host who authors and runs quizzes and players who join live
games without accounts.

## User Stories

### US-001: Host a quiz

**As a**
host

**I want**
to author quizzes and control live games

**So that**
I can run an interactive quiz for players.

### US-002: Play without an account

**As a**
player

**I want**
to join a shared game with a handle name

**So that**
I can participate without registering or logging in.

## Acceptance Criteria

- Given a host account, when the host logs in with a username and password, then the host can create, edit, and delete quizzes.
- Given a valid quiz, when the host runs it, then the host can receive and share a short unique game PIN or join link.
- Given players in the lobby, when the host manages the game, then the host can see joining players, remove an inappropriate player, start the quiz, start or end questions, see answer counts, display question statistics and leaderboards, continue to later questions, end the game, and view final results.
- Given a shared link or PIN, when a player supplies a handle name, then the player joins the lobby and waits for the host.
- Given a joined player, when the host starts questions, then the player receives them in real time and can submit one answer per question.
- Given a submitted answer, when it is processed and later revealed, then the player first sees whether it was accepted and sees whether it was correct only after the reveal, followed by points and leaderboard updates.
- Given an active game, when play continues until the host ends it, then the player sees the final results.

## Business Rules

### FR-1: Roles

- The system has exactly two roles: host and player.

### FR-1.1: Host

- A host is an authenticated account holder who authors quizzes and runs live games.
- A host can create questions, add 2–6 answer options, mark one or more options correct, and configure each question's duration and points.

### FR-1.2: Player

- A player is a person who plays a game.
- Players have no account and never log in.

## Edge Cases

- A player journey must never require account creation or authentication.
- A player can submit only one answer for each question.
