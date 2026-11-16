import { describe, expect, it } from 'vitest';
import type { TaskListItem } from '../../api/types';
import { getVisibleRange, groupByDeadline, isInSameMonth, shiftAnchor } from './calendarLogic';

describe('getVisibleRange', () => {
  it('shows whole weeks (Monday to Sunday) around a month', () => {
    // November 2026 starts on Sunday 1st and ends on Monday 30th.
    const range = getVisibleRange('month', '2026-11-15');

    expect(range.from).toBe('2026-10-26');
    expect(range.to).toBe('2026-12-06');
    expect(range.days).toHaveLength(42);
    expect(range.days[0]).toBe('2026-10-26');
  });

  it('uses five rows when the month fits into five weeks', () => {
    // March 2027 starts on Monday 1st and ends on Wednesday 31st.
    expect(getVisibleRange('month', '2027-03-10').days).toHaveLength(35);
  });

  it('shows seven days for the week of the anchor', () => {
    const range = getVisibleRange('week', '2026-11-05'); // Thursday

    expect(range.from).toBe('2026-11-02');
    expect(range.to).toBe('2026-11-08');
    expect(range.days).toHaveLength(7);
  });
});

describe('shiftAnchor', () => {
  it('moves by one month or one week', () => {
    expect(shiftAnchor('month', '2026-11-15', 1)).toBe('2026-12-15');
    expect(shiftAnchor('month', '2026-01-31', 1)).toBe('2026-02-28');
    expect(shiftAnchor('month', '2026-11-15', -1)).toBe('2026-10-15');
    expect(shiftAnchor('week', '2026-11-05', 1)).toBe('2026-11-12');
    expect(shiftAnchor('week', '2026-11-05', -1)).toBe('2026-10-29');
  });
});

describe('isInSameMonth', () => {
  it('compares year and month', () => {
    expect(isInSameMonth('2026-11-30', '2026-11-01')).toBe(true);
    expect(isInSameMonth('2026-12-01', '2026-11-01')).toBe(false);
    expect(isInSameMonth('2025-11-10', '2026-11-01')).toBe(false);
  });
});

describe('groupByDeadline', () => {
  const base: TaskListItem = {
    id: '1',
    title: 'x',
    description: null,
    status: 'Todo',
    priority: 'Low',
    deadline: null,
    position: 1,
    createdAt: '2026-11-01T00:00:00Z',
    updatedAt: '2026-11-01T00:00:00Z',
    completedAt: null,
    isOverdue: false,
    tags: [],
    subtasksTotal: 0,
    subtasksDone: 0,
  };

  it('groups tasks by date and skips tasks without a deadline', () => {
    const groups = groupByDeadline([
      { ...base, id: '1', deadline: '2026-11-05' },
      { ...base, id: '2', deadline: '2026-11-05' },
      { ...base, id: '3', deadline: '2026-11-06' },
      { ...base, id: '4', deadline: null },
    ]);

    expect(groups.get('2026-11-05')?.map((t) => t.id)).toEqual(['1', '2']);
    expect(groups.get('2026-11-06')?.map((t) => t.id)).toEqual(['3']);
    expect(groups.size).toBe(2);
  });
});
