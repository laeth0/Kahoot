import { type KeyboardEvent, type PointerEvent, useEffect, useRef, useState } from 'react';

const SCROLL_EDGE_SIZE = 100;
const MAX_SCROLL_SPEED = 900;
const PREVIEW_MARGIN = 12;
const DESTINATION_CLEARANCE = 40;

interface QuestionDrag {
  questionId: string;
  sourceIndex: number;
  handle: HTMLElement;
  pointerId: number | null;
  initialX: number;
  initialY: number;
  offsetX: number;
  offsetY: number;
  width: number;
}

interface QuestionDestination {
  index: number;
  beforeIndex: number | null;
  top: number;
}

interface UseQuestionDragOptions {
  questionIds: string[];
  disabled: boolean;
  onReorder: (orderedQuestionIds: string[]) => void;
}

export function useQuestionDrag({ questionIds, disabled, onReorder }: UseQuestionDragOptions) {
  const listRef = useRef<HTMLDivElement>(null);
  const previewRef = useRef<HTMLDivElement>(null);
  const [drag, setDrag] = useState<QuestionDrag | null>(null);
  const [destination, setDestination] = useState<QuestionDestination | null>(null);
  const [announcement, setAnnouncement] = useState('');

  function startDrag(questionId: string, handle: HTMLElement, pointer?: PointerEvent) {
    if (disabled || drag || questionIds.length < 2) return;
    const card = handle.closest<HTMLElement>('[data-question-id]');
    if (!card) return;
    const rect = card.getBoundingClientRect();
    const sourceIndex = questionIds.indexOf(questionId);
    if (sourceIndex < 0) return;

    if (pointer) handle.setPointerCapture(pointer.pointerId);
    setDestination(null);
    setDrag({
      questionId,
      sourceIndex,
      handle,
      pointerId: pointer?.pointerId ?? null,
      initialX: pointer?.clientX ?? rect.left,
      initialY: pointer?.clientY ?? rect.top,
      offsetX: pointer ? pointer.clientX - rect.left : 0,
      offsetY: pointer ? pointer.clientY - rect.top : 0,
      width: rect.width,
    });
    setAnnouncement(`Picked up Question #${sourceIndex + 1}. Escape cancels the move.`);
  }

  function onPointerDown(event: PointerEvent<HTMLElement>, questionId: string) {
    if (!event.isPrimary || event.button !== 0) return;
    event.preventDefault();
    event.currentTarget.focus({ preventScroll: true });
    startDrag(questionId, event.currentTarget, event);
  }

  function onKeyDown(event: KeyboardEvent<HTMLElement>, questionId: string) {
    if (drag || (event.key !== ' ' && event.key !== 'Enter')) return;
    event.preventDefault();
    event.stopPropagation();
    startDrag(questionId, event.currentTarget);
  }

  useEffect(() => {
    if (!drag || disabled) return;
    const listElement = listRef.current;
    if (!listElement) return;
    const list = listElement;
    const activeDrag = drag;
    const cards = Array.from(list.querySelectorAll<HTMLElement>('[data-question-id]'));
    const remainingCards = cards.filter((card) => card.dataset.questionId !== drag.questionId);
    const remainingIds = questionIds.filter((id) => id !== drag.questionId);
    let pointerX = drag.initialX;
    let pointerY = drag.initialY;
    let destinationIndex = drag.sourceIndex;
    let isValidDestination = true;
    let finished = false;
    let animationFrame = 0;
    let previousTime = performance.now();
    const previousCursor = document.body.style.cursor;
    const previousUserSelect = document.body.style.userSelect;
    document.body.style.cursor = 'grabbing';
    document.body.style.userSelect = 'none';

    function measureDestination() {
      const listRect = list.getBoundingClientRect();
      if (activeDrag.pointerId !== null) {
        isValidDestination =
          pointerX >= listRect.left &&
          pointerX <= listRect.right &&
          pointerY >= 0 &&
          pointerY <= window.innerHeight;
        const nextIndex = remainingCards.findIndex((card) => {
          const rect = card.getBoundingClientRect();
          return pointerY < rect.top + rect.height / 2;
        });
        destinationIndex = nextIndex < 0 ? remainingCards.length : nextIndex;
      }

      if (!isValidDestination) {
        setDestination(null);
        return;
      }

      const beforeCard = remainingCards[destinationIndex];
      const lastCard = remainingCards.at(-1);
      const top = beforeCard
        ? beforeCard.getBoundingClientRect().top - listRect.top
        : (lastCard?.getBoundingClientRect().bottom ?? listRect.bottom) - listRect.top;
      const beforeIndex = beforeCard
        ? questionIds.findIndex((id) => id === beforeCard.dataset.questionId)
        : null;
      setDestination((previous) =>
        previous?.index === destinationIndex && previous.top === top
          ? previous
          : { index: destinationIndex, beforeIndex, top },
      );
    }

    function positionPreview() {
      const preview = previewRef.current;
      if (!preview) return;
      const target = remainingCards[destinationIndex] ?? remainingCards.at(-1);
      const targetRect = target?.getBoundingClientRect();
      const x =
        activeDrag.pointerId === null
          ? (targetRect?.left ?? pointerX)
          : pointerX - activeDrag.offsetX;
      const y =
        activeDrag.pointerId === null
          ? (targetRect?.top ?? pointerY)
          : pointerY - activeDrag.offsetY;
      const left = Math.max(
        PREVIEW_MARGIN,
        Math.min(x, window.innerWidth - preview.offsetWidth - PREVIEW_MARGIN),
      );
      let top = Math.max(
        PREVIEW_MARGIN,
        Math.min(y, window.innerHeight - preview.offsetHeight - PREVIEW_MARGIN),
      );
      const destinationY = remainingCards[destinationIndex] ? targetRect?.top : targetRect?.bottom;
      if (
        destinationY !== undefined &&
        top < destinationY + DESTINATION_CLEARANCE &&
        top + preview.offsetHeight > destinationY - DESTINATION_CLEARANCE
      ) {
        const below = destinationY + DESTINATION_CLEARANCE;
        const above = destinationY - DESTINATION_CLEARANCE - preview.offsetHeight;
        if (below + preview.offsetHeight <= window.innerHeight - PREVIEW_MARGIN) top = below;
        else if (above >= PREVIEW_MARGIN) top = above;
      }
      preview.style.transform = `translate3d(${left}px, ${top}px, 0)`;
      preview.style.visibility = 'visible';
    }

    function tick(time: number) {
      if (finished) return;
      const seconds = Math.min((time - previousTime) / 1000, 0.05);
      previousTime = time;
      if (activeDrag.pointerId !== null) {
        const listRect = list.getBoundingClientRect();
        if (pointerX >= listRect.left && pointerX <= listRect.right) {
          const topSpeed = Math.max(0, 1 - pointerY / SCROLL_EDGE_SIZE);
          const bottomSpeed = Math.max(0, 1 - (window.innerHeight - pointerY) / SCROLL_EDGE_SIZE);
          const speed = Math.max(-1, Math.min(1, bottomSpeed - topSpeed));
          if (speed !== 0)
            window.scrollBy({ top: speed * MAX_SCROLL_SPEED * seconds, behavior: 'instant' });
        }
      }
      measureDestination();
      positionPreview();
      animationFrame = requestAnimationFrame(tick);
    }

    function finish(save: boolean) {
      if (finished) return;
      finished = true;
      if (save) measureDestination();
      setDrag(null);
      setDestination(null);
      if (save && isValidDestination && destinationIndex !== activeDrag.sourceIndex) {
        const orderedIds = [...remainingIds];
        orderedIds.splice(destinationIndex, 0, activeDrag.questionId);
        setAnnouncement(`Question moved to position ${destinationIndex + 1}. Saving order.`);
        onReorder(orderedIds);
      } else {
        setAnnouncement('Question order unchanged.');
      }
      activeDrag.handle.focus({ preventScroll: true });
    }

    function move(event: globalThis.PointerEvent) {
      if (event.pointerId !== activeDrag.pointerId) return;
      pointerX = event.clientX;
      pointerY = event.clientY;
    }

    function drop(event: globalThis.PointerEvent) {
      if (event.pointerId !== activeDrag.pointerId) return;
      move(event);
      finish(true);
    }

    function cancel() {
      finish(false);
    }

    function keyDown(event: globalThis.KeyboardEvent) {
      if (event.key === 'Escape' || event.key === 'Tab') {
        if (event.key === 'Escape') event.preventDefault();
        cancel();
        return;
      }
      if (activeDrag.pointerId !== null) return;
      if (event.key === ' ' || event.key === 'Enter') {
        event.preventDefault();
        finish(true);
        return;
      }
      const nextIndex = {
        ArrowUp: destinationIndex - 1,
        ArrowDown: destinationIndex + 1,
        Home: 0,
        End: remainingIds.length,
      }[event.key];
      if (nextIndex === undefined) return;
      event.preventDefault();
      destinationIndex = Math.max(0, Math.min(remainingIds.length, nextIndex));
      const target = remainingCards[destinationIndex] ?? remainingCards.at(-1);
      target?.scrollIntoView({ block: 'center', behavior: 'instant' });
      measureDestination();
      positionPreview();
    }

    window.addEventListener('pointermove', move);
    window.addEventListener('pointerup', drop);
    window.addEventListener('pointercancel', cancel);
    window.addEventListener('keydown', keyDown);
    window.addEventListener('blur', cancel);
    drag.handle.addEventListener('lostpointercapture', cancel);
    animationFrame = requestAnimationFrame(tick);

    return () => {
      finished = true;
      cancelAnimationFrame(animationFrame);
      window.removeEventListener('pointermove', move);
      window.removeEventListener('pointerup', drop);
      window.removeEventListener('pointercancel', cancel);
      window.removeEventListener('keydown', keyDown);
      window.removeEventListener('blur', cancel);
      drag.handle.removeEventListener('lostpointercapture', cancel);
      if (drag.pointerId !== null && drag.handle.hasPointerCapture(drag.pointerId)) {
        drag.handle.releasePointerCapture(drag.pointerId);
      }
      document.body.style.cursor = previousCursor;
      document.body.style.userSelect = previousUserSelect;
    };
  }, [drag, disabled, questionIds, onReorder]);

  return { listRef, previewRef, drag, destination, announcement, onPointerDown, onKeyDown };
}
