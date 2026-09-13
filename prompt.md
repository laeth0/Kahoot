
Improve the quiz image experience for both players and hosts.

## New Requirement

Change the quiz image rules:

- Question images are allowed.
- Answer choice images are NOT allowed.
- Answers must contain text only.

Remove any existing support for displaying images inside answer choices during gameplay.

---

# Objective

When a question contains an image, display it clearly and prominently so that both the player and host can easily understand it.

The image should not appear as a small thumbnail.

The main priority is:

**Make question images large, clear, and easy to see during live gameplay.**

---

# Player Experience (Highest Priority)

Improve the player quiz screen.

When a question has an image:

- Display the image in a large, clear area.
- Place it in a visually important position near the question text.
- Ensure players can easily understand the image before selecting an answer.
- Do not require the player to click a button to view the image.
- Do not hide the image behind a small preview.

The player should immediately see:

```

Question Text

+----------------------+
|                      |
|    Large Image       |
|                      |
+----------------------+

Answer A
Answer B
Answer C
Answer D

```

The answer choices should remain text-only.

---

# Host Experience

Apply the same image improvement to the Host game view.

When hosting a question with an image:

- Show the image clearly and prominently.
- Make it easy for the host and audience to see.
- Keep the timer, question information, and controls visible.
- Maintain the existing host layout and design.

Do not create different image behavior between Host and Player.

---

# Answer Choices

Enforce this rule everywhere:

Answer choices support text only.

Requirements:

- Remove image rendering from answer cards.
- Do not show answer thumbnails.
- Do not show image placeholders inside answers.
- Do not show broken image icons for answers.
- Keep answer selection simple and text-based.

Example:

Before:

```

[B] [small image] Python

```

After:

```

[B] Python

```

---

# Question Image UI Requirements

The question image component should:

- Display large enough to understand.
- Maintain aspect ratio.
- Never stretch or distort images.
- Support different image dimensions.
- Work with screenshots, diagrams, photos, and illustrations.
- Have rounded corners consistent with the application design.
- Have proper spacing from question text and answers.

Use responsive sizing based on available screen space.

Do not use fixed sizes that break on different devices.

---

# Responsive Design

The image display must work correctly on:

- desktop
- laptop
- projector screens
- tablets
- mobile phones

Adapt based on:

- viewport width
- viewport height
- image aspect ratio

Avoid:

- unnecessary scrolling
- overlapping content
- images pushing important controls outside the viewport

The timer and answer buttons must remain usable.

---

# Image Handling Validation

Verify the complete flow:

Upload question image
→ Backend storage
→ Database reference
→ API response
→ Host rendering
→ Player rendering

Ensure:

- image URLs are correct
- images load correctly
- broken images have a fallback
- old answer images are not rendered anymore

---

# Backend / Data Rules

If the backend currently allows images for answers:

Review whether this should be restricted.

The final business rule should be:

- Question:
  - Text ✅
  - Image ✅ optional

- Answer Choice:
  - Text ✅
  - Image ❌ not supported

Do not allow creating new questions with answer images.

If existing data contains answer images:
- handle it gracefully
- do not break old quizzes
- hide/remove answer image rendering from gameplay

---

# Validation

Test:

1. Question without image.
2. Question with image.
3. Multiple-choice answers with text only.
4. Existing questions that previously had answer images.
5. Host gameplay.
6. Player gameplay.
7. Mobile screen.
8. Desktop screen.

Verify:

- Question images are large and clear.
- Players immediately understand the image.
- Hosts can clearly see the image.
- Answers are always text-only.
- No broken image placeholders appear.
- Timer and controls remain accessible.
- Layout remains responsive.

Prioritize player understanding and fast answering during live gameplay.
