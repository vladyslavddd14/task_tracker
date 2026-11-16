import { describe, expect, it } from 'vitest';
import type { TaskListItem, TaskStatus } from '../../api/types';
import { POSITION_STEP, computeMove, groupByStatus, resolveDropTarget } from './boardLogic';

function task(id: string, status: TaskStatus, position: number): TaskListItem {
  return {
    id,
    title: id,
    description: null,
    status,
    priority: 'Medium',
    deadline: null,
    position,
    createdAt: '2026-11-01T10:00:00Z',
    updatedAt: '2026-11-01T10:00:00Z',
    completedAt: null,
    isOverdue: false,
    tags: [],
    subtasksTotal: 0,
    subtasksDone: 0,
  };
}

const tasks = [
  task('b', 'Todo', 2000),
  task('a', 'Todo', 1000),
  task('c', 'Todo', 3000),
  task('x', 'InProgress', 1000),
  task('y', 'InProgress', 2000),
];

describe('groupByStatus', () => {
  it('puts tasks into three columns ordered by position', () => {
    const columns = groupByStatus(tasks);

    expect(columns.Todo.map((t) => t.id)).toEqual(['a', 'b', 'c']);
    expect(columns.InProgress.map((t) => t.id)).toEqual(['x', 'y']);
    expect(columns.Done).toEqual([]);
  });
});

describe('computeMove', () => {
  const columns = groupByStatus(tasks);

  it('places a card between two neighbours at the midpoint of their positions', () => {
    // Move "c" to index 1 of Todo: between "a" (1000) and "b" (2000).
    expect(computeMove(columns, 'c', { status: 'Todo', index: 1 })).toEqual({
      status: 'Todo',
      position: 1500,
    });
  });

  it('places a card at the start of a column before the first card', () => {
    expect(computeMove(columns, 'c', { status: 'Todo', index: 0 }).position).toBe(
      1000 - POSITION_STEP,
    );
  });

  it('places a card at the end of a column after the last card', () => {
    expect(computeMove(columns, 'a', { status: 'Todo', index: 99 }).position).toBe(
      3000 + POSITION_STEP,
    );
  });

  it('moves a card into another column and changes its status', () => {
    expect(computeMove(columns, 'a', { status: 'InProgress', index: 1 })).toEqual({
      status: 'InProgress',
      position: 1500,
    });
  });

  it('uses the default position for an empty column', () => {
    expect(computeMove(columns, 'a', { status: 'Done', index: 0 })).toEqual({
      status: 'Done',
      position: POSITION_STEP,
    });
  });

  it('ignores the moved card itself when it looks for neighbours', () => {
    // "b" is already at index 1; dropping it at index 1 keeps it between "a" and "c".
    expect(computeMove(columns, 'b', { status: 'Todo', index: 1 }).position).toBe(2000);
  });

  it('keeps the order consistent after the move', () => {
    const move = computeMove(columns, 'c', { status: 'Todo', index: 1 });
    const moved = tasks.map((t) => (t.id === 'c' ? { ...t, ...move } : t));

    expect(groupByStatus(moved).Todo.map((t) => t.id)).toEqual(['a', 'c', 'b']);
  });
});

describe('resolveDropTarget', () => {
  const columns = groupByStatus(tasks);

  it('drops on a card at the place of that card', () => {
    expect(resolveDropTarget(columns, 'b')).toEqual({ status: 'Todo', index: 1 });
    expect(resolveDropTarget(columns, 'y')).toEqual({ status: 'InProgress', index: 1 });
  });

  it('drops on a column at its end', () => {
    expect(resolveDropTarget(columns, 'column:InProgress')).toEqual({
      status: 'InProgress',
      index: 2,
    });
    expect(resolveDropTarget(columns, 'column:Done')).toEqual({ status: 'Done', index: 0 });
  });

  it('returns null for unknown targets', () => {
    expect(resolveDropTarget(columns, 'nope')).toBeNull();
    expect(resolveDropTarget(columns, 'column:Archive')).toBeNull();
  });
});
