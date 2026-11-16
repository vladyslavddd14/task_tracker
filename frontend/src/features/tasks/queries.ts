import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../api/endpoints';
import type {
  CreateTaskRequest,
  TaskDetails,
  TaskFilters,
  TaskListItem,
  TaskStatus,
  UpdateTaskRequest,
} from '../../api/types';

// Every task query key starts with 'tasks', so one invalidation refreshes list, board, calendar and details.
export const taskKeys = {
  all: ['tasks'] as const,
  list: (filters: TaskFilters, page: number, pageSize: number) =>
    ['tasks', 'list', filters, page, pageSize] as const,
  board: (filters: TaskFilters) => ['tasks', 'board', filters] as const,
  today: ['tasks', 'today'] as const,
  calendar: (filters: TaskFilters, from: string, to: string) =>
    ['tasks', 'calendar', filters, from, to] as const,
  undated: (filters: TaskFilters) => ['tasks', 'undated', filters] as const,
  detail: (id: string) => ['tasks', 'detail', id] as const,
};

const BOARD_PAGE_SIZE = 200;

export function useTaskList(filters: TaskFilters, page: number, pageSize: number) {
  return useQuery({
    queryKey: taskKeys.list(filters, page, pageSize),
    queryFn: () => api.tasks.list(filters, page, pageSize),
    placeholderData: (previous) => previous,
  });
}

export function useTodayTasks() {
  return useQuery({ queryKey: taskKeys.today, queryFn: api.tasks.today });
}

/** Loads every task that matches the filters (page after page), because a kanban board shows them all. */
async function fetchAllTasks(filters: TaskFilters): Promise<TaskListItem[]> {
  const all: TaskListItem[] = [];
  for (let page = 1; ; page++) {
    const result = await api.tasks.list(filters, page, BOARD_PAGE_SIZE);
    all.push(...result.items);
    if (result.items.length === 0 || all.length >= result.total) return all;
  }
}

export function useBoardTasks(filters: TaskFilters) {
  return useQuery({
    queryKey: taskKeys.board(filters),
    queryFn: () => fetchAllTasks(filters),
  });
}

export function useCalendarTasks(filters: TaskFilters, from: string, to: string) {
  return useQuery({
    queryKey: taskKeys.calendar(filters, from, to),
    queryFn: () => api.tasks.calendar(filters, from, to),
    placeholderData: (previous) => previous,
  });
}

/** Tasks without a deadline for the "No date" side panel of the calendar. */
export function useUndatedTasks(filters: TaskFilters) {
  const undatedFilters: TaskFilters = {
    ...filters,
    deadline: 'none',
    deadlineFrom: '',
    deadlineTo: '',
  };
  return useQuery({
    queryKey: taskKeys.undated(undatedFilters),
    queryFn: () => fetchAllTasks(undatedFilters),
  });
}

export function useTaskDetails(id: string) {
  return useQuery({ queryKey: taskKeys.detail(id), queryFn: () => api.tasks.get(id) });
}

export function useTags() {
  return useQuery({ queryKey: ['tags'], queryFn: api.tags.list });
}

export function useCustomFields() {
  return useQuery({ queryKey: ['custom-fields'], queryFn: api.customFields.list });
}

export function useCreateTask() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateTaskRequest) => api.tasks.create(body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: taskKeys.all }),
  });
}

export function useUpdateTask(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: UpdateTaskRequest) => api.tasks.update(id, body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: taskKeys.all }),
  });
}

export function useChangeStatus() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, status }: { id: string; status: TaskStatus }) =>
      api.tasks.changeStatus(id, status),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: taskKeys.all }),
  });
}

export function useDeleteTask() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.tasks.remove(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: taskKeys.all }),
  });
}

interface MoveVariables {
  id: string;
  status: TaskStatus;
  position: number;
}

/** Kanban move with an optimistic update: the card moves at once and jumps back if the server refuses. */
export function useMoveTask() {
  const queryClient = useQueryClient();
  const boardKey = ['tasks', 'board'] as const;

  return useMutation({
    mutationFn: ({ id, status, position }: MoveVariables) => api.tasks.move(id, status, position),
    onMutate: async ({ id, status, position }) => {
      await queryClient.cancelQueries({ queryKey: boardKey });
      const snapshots = queryClient.getQueriesData<TaskListItem[]>({ queryKey: boardKey });
      queryClient.setQueriesData<TaskListItem[]>({ queryKey: boardKey }, (tasks) =>
        tasks?.map((task) => (task.id === id ? { ...task, status, position } : task)),
      );
      return { snapshots };
    },
    onError: (_error, _variables, context) => {
      context?.snapshots.forEach(([key, data]) =>
        queryClient.setQueryData<TaskListItem[] | undefined>(key, data),
      );
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: taskKeys.all }),
  });
}

/** Changes only the deadline of a task (used by drag-and-drop in the calendar). */
export function useSetDeadline() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, deadline }: { id: string; deadline: string }) => {
      // The update endpoint replaces all fields, so start from the current state of the task.
      const task: TaskDetails = await api.tasks.get(id);
      return api.tasks.update(id, {
        title: task.title,
        description: task.description,
        notesMarkdown: task.notesMarkdown,
        status: task.status,
        priority: task.priority,
        deadline,
        tagIds: task.tags.map((tag) => tag.id),
        customFields: task.customFields.map((field) => ({
          definitionId: field.definitionId,
          value: field.value,
        })),
      });
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: taskKeys.all }),
  });
}

export function useSubtaskActions(taskId: string) {
  const queryClient = useQueryClient();
  const refresh = () => queryClient.invalidateQueries({ queryKey: taskKeys.all });

  return {
    add: useMutation({
      mutationFn: (title: string) => api.subtasks.add(taskId, title),
      onSuccess: refresh,
    }),
    update: useMutation({
      mutationFn: ({ id, title, isDone }: { id: string; title: string; isDone: boolean }) =>
        api.subtasks.update(taskId, id, title, isDone),
      onSuccess: refresh,
    }),
    remove: useMutation({
      mutationFn: (id: string) => api.subtasks.remove(taskId, id),
      onSuccess: refresh,
    }),
    reorder: useMutation({
      mutationFn: (orderedIds: string[]) => api.subtasks.reorder(taskId, orderedIds),
      onSuccess: refresh,
    }),
  };
}
