import { request, toQuery } from './client';
import type {
  AuthResponse,
  CreateTaskRequest,
  CustomFieldDefinition,
  CustomFieldType,
  PagedResult,
  ProfileTemplate,
  Subtask,
  Tag,
  TaskDetails,
  TaskFilters,
  TaskListItem,
  TaskStatus,
  UpdateTaskRequest,
  User,
} from './types';

/** Converts the filter state of the UI into API query parameters. */
export function filtersToParams(filters: TaskFilters): Record<string, string | string[]> {
  const params: Record<string, string | string[]> = {};
  if (filters.status.length > 0) params.status = filters.status;
  if (filters.priority) params.priority = filters.priority;
  if (filters.tagIds.length > 0) params.tagId = filters.tagIds;
  if (filters.deadline === 'range') {
    if (filters.deadlineFrom) params.deadlineFrom = filters.deadlineFrom;
    if (filters.deadlineTo) params.deadlineTo = filters.deadlineTo;
  } else if (filters.deadline) {
    params.deadline = filters.deadline;
  }
  if (filters.q.trim()) params.q = filters.q.trim();
  if (filters.customFieldId && filters.customFieldValue.trim()) {
    params.customFieldId = filters.customFieldId;
    params.customFieldValue = filters.customFieldValue.trim();
  }
  params.sort = filters.sort;
  if (filters.dir) params.dir = filters.dir;
  return params;
}

export const api = {
  auth: {
    login: (email: string, password: string) =>
      request<AuthResponse>('POST', '/auth/login', { email, password }),
    register: (body: {
      email: string;
      password: string;
      displayName: string;
      timeZoneId: string;
      profileTemplate: ProfileTemplate;
    }) => request<AuthResponse>('POST', '/auth/register', body),
  },

  me: {
    get: () => request<User>('GET', '/me'),
    update: (displayName: string, timeZoneId: string) =>
      request<User>('PUT', '/me', { displayName, timeZoneId }),
    changePassword: (currentPassword: string, newPassword: string) =>
      request<void>('PUT', '/me/password', { currentPassword, newPassword }),
    applyTemplate: (template: ProfileTemplate) =>
      request<User>('POST', '/me/apply-template', { template }),
  },

  tasks: {
    list: (filters: TaskFilters, page: number, pageSize: number) =>
      request<PagedResult<TaskListItem>>(
        'GET',
        `/tasks${toQuery({ ...filtersToParams(filters), page, pageSize })}`,
      ),
    today: () => request<TaskListItem[]>('GET', '/tasks/today'),
    calendar: (filters: TaskFilters, from: string, to: string) =>
      request<TaskListItem[]>(
        'GET',
        `/tasks/calendar${toQuery({ ...filtersToParams(filters), from, to })}`,
      ),
    get: (id: string) => request<TaskDetails>('GET', `/tasks/${id}`),
    create: (body: CreateTaskRequest) => request<TaskDetails>('POST', '/tasks', body),
    update: (id: string, body: UpdateTaskRequest) =>
      request<TaskDetails>('PUT', `/tasks/${id}`, body),
    changeStatus: (id: string, status: TaskStatus) =>
      request<TaskDetails>('PATCH', `/tasks/${id}/status`, { status }),
    move: (id: string, status: TaskStatus, position: number) =>
      request<TaskDetails>('PATCH', `/tasks/${id}/move`, { status, position }),
    remove: (id: string) => request<void>('DELETE', `/tasks/${id}`),
  },

  subtasks: {
    add: (taskId: string, title: string) =>
      request<Subtask>('POST', `/tasks/${taskId}/subtasks`, { title, isDone: false }),
    update: (taskId: string, id: string, title: string, isDone: boolean) =>
      request<Subtask>('PUT', `/tasks/${taskId}/subtasks/${id}`, { title, isDone }),
    remove: (taskId: string, id: string) =>
      request<void>('DELETE', `/tasks/${taskId}/subtasks/${id}`),
    reorder: (taskId: string, orderedIds: string[]) =>
      request<Subtask[]>('PATCH', `/tasks/${taskId}/subtasks/reorder`, { orderedIds }),
  },

  tags: {
    list: () => request<Tag[]>('GET', '/tags'),
    create: (name: string, color: string | null) => request<Tag>('POST', '/tags', { name, color }),
    update: (id: string, name: string, color: string | null) =>
      request<Tag>('PUT', `/tags/${id}`, { name, color }),
    remove: (id: string) => request<void>('DELETE', `/tags/${id}`),
  },

  customFields: {
    list: () => request<CustomFieldDefinition[]>('GET', '/custom-fields'),
    create: (name: string, type: CustomFieldType) =>
      request<CustomFieldDefinition>('POST', '/custom-fields', { name, type }),
    update: (id: string, name: string, type: CustomFieldType, position: number) =>
      request<CustomFieldDefinition>('PUT', `/custom-fields/${id}`, { name, type, position }),
    remove: (id: string) => request<void>('DELETE', `/custom-fields/${id}`),
  },
};
