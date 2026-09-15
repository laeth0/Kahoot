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
- Given a choice, when the host configures it, then the choice contains required text and a correctness flag.
- Given a question with multiple correct choices, when an answer is evaluated, then it is correct only when the selected choices exactly match all expected correct choices.
- Given a valid draft quiz, when the host publishes it, then the quiz becomes eligible to start a game.
- Given an authenticated host, when the host lists quizzes, then only that host's quizzes are returned newest first with their publication state and question count.
- Given an authenticated host and an owned quiz, when the host retrieves it, then its questions and choices are returned in their stored order.
- Given an editable quiz, when its metadata, questions, choices, or question order change, then the quiz becomes unpublished.
- Given a complete ordered list of the quiz's question IDs, when the host reorders it, then only the order indexes change.

## Business Rules

### FR-3: Quiz authoring

- A quiz has a title, an optional description, an `isPublished` flag, and an ordered list of questions.
- A question has text, an optional image, an order, a time limit, base points, and 2–6 choices.
- A choice has required text, an order, and an `isCorrect` flag.
- G2 correct-answer cardinality permits one or more correct choices per question (`1..N`).
- A submitted answer is correct only when it contains every correct choice and no incorrect choice.
- A quiz must be published before a game can be started from it.
- A quiz that has ever been used to run a game cannot be deleted, preserving historical results.
- A quiz with a game session that is not finished cannot be edited.

### FR-3.2: Publish validation

- Every question must have valid text.
- Every question must contain text of at most 500 characters, a time limit from 5 through 300 seconds, and non-negative base points.
- Every question must have 2–6 choices and at least one correct choice.
- Every choice must contain non-whitespace text of at most 300 characters.

### FR-3.3: Quiz queries

- Hosts can list only their own quizzes; each summary contains the ID, title, optional description, publication state, and question count.
- Quiz summaries are ordered newest first.
- Hosts can retrieve only their own quiz details.
- Quiz details return questions and choices ordered by their `OrderIndex` values.

### FR-3.4: Quiz mutations

- A quiz title is required and limited to 200 characters; its optional description is limited to 1,000 characters.
- Quiz titles and non-empty descriptions are trimmed, and a blank description is stored as absent.
- Creating a quiz associates it with the authenticated host.
- Updating quiz metadata unpublishes the quiz.
- Deleting a quiz is allowed only when it has never been used for a game session.

### FR-3.5: Question mutations and ordering

- A newly added question is appended after the quiz's highest existing order index.
- Adding, updating, deleting, or reordering questions unpublishes the quiz.
- Deleting a question compacts every following order index so the sequence remains contiguous.
- Reordering requires a non-empty, duplicate-free list containing exactly every question ID currently in the quiz.
- Question reordering preserves question and choice identities and changes only question order indexes.
- Every quiz mutation is restricted to the authenticated owner.
- Before a mutation, unfinished sessions without a connected host or active disconnect grace period are marked finished. Any remaining unfinished session blocks the mutation.

## Edge Cases

- Publishing is rejected when any question or choice violates the publish-validation rules.
- Publishing is rejected when the quiz has no questions.
- Editing is rejected while an unfinished session still has a connected host or is within the host-disconnect grace period.
- Deletion is rejected after the quiz has been used by any game session, including a finished session.
- Selecting all but one correct choice, or selecting any extra incorrect choice, produces an incorrect answer.
- A missing quiz or a quiz owned by another host is reported as not found.
- Updating or deleting a question that does not belong to the quiz is reported as `Quiz.QuestionNotFound`.
- A reorder request whose ID set does not exactly match the quiz is rejected with `Quiz.QuestionSetMismatch`.
- Concurrent question insertion at the same order index is rejected with `Quiz.ConcurrentModification`.
