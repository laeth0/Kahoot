# Quiz and Question Management

## Overview

Hosts build ordered quizzes from configurable questions and choices, validate them,
and publish them before starting a game.

## User Stories

### US-005: Author a quiz

**As a**
host

**I want**
to create and edit quizzes with ordered questions and answer choices

**So that**
I can prepare the content for a live game.

### US-006: Publish a valid quiz

**As a**
host

**I want**
invalid quiz content to be identified before publication

**So that**
players receive complete and answerable questions.

## Acceptance Criteria

- Given a draft quiz, when the host manages it, then the host can set its title, optional description, publication state, and ordered question list.
- Given a question, when the host configures it, then the host can set its text, optional image, order, time limit, base points, and 2–6 choices.
- Given a choice, when the host configures it, then the choice contains text, an image, or both and has a correctness flag.
- Given a question with multiple correct choices, when an answer is evaluated, then it is correct only when the selected choices exactly match all expected correct choices.
- Given a valid draft quiz, when the host publishes it, then the quiz becomes eligible to start a game.

## Business Rules

### FR-3: Quiz authoring

- A quiz has a title, an optional description, an `isPublished` flag, and an ordered list of questions.
- A question has text, an optional image, an order, a time limit, base points, and 2–6 choices.
- A choice has text and/or an image, at least one of which is required, and an `isCorrect` flag.
- G2 correct-answer cardinality permits one or more correct choices per question (`1..N`).
- A submitted answer is correct only when it contains every correct choice and no incorrect choice.
- A quiz must be published before a game can be started from it.
- A quiz that has ever been used to run a game cannot be deleted, preserving historical results.
- A quiz with a game session that is not finished cannot be edited.

### FR-3.2: Publish validation

- Every question must have valid text.
- Every question must have a time limit within the allowed range and non-negative base points.
- Every question must have 2–6 choices and at least one correct choice.
- Every choice must contain text or an image.

## Edge Cases

- Publishing is rejected when any question or choice violates the publish-validation rules.
- Editing is rejected while any game session for the quiz is unfinished.
- Deletion is rejected after the quiz has been used by any game session, including a finished session.
- Selecting all but one correct choice, or selecting any extra incorrect choice, produces an incorrect answer.
