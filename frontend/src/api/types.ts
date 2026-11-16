// Types of the REST API (see the backend DTOs).

export type TaskStatus = 'Todo' | 'InProgress' | 'Done';
export type TaskPriority = 'Low' | 'Medium' | 'High';
export type CustomFieldType = 'Text' | 'Number' | 'Checkbox' | 'Date';
export type ProfileTemplate = 'Universal' | 'Student' | 'Freelancer' | 'Training';
export type DeadlineMode = 'overdue' | 'today' | 'upcoming' | 'none';
export type SortField = 'priority' | 'deadline' | 'createdAt' | 'title';
export type SortDirection = 'asc' | 'desc';

export const TASK_STATUSES: TaskStatus[] = ['Todo', 'InProgress', 'Done'];
export const TASK_PRIORITIES: TaskPriority[] = ['Low', 'Medium', 'High'];
export const PROFILE_TEMPLATES: ProfileTemplate[] = [
  'Universal',
  'Student',
  'Freelancer',
  'Training',
];
export const CUSTOM_FIELD_TYPES: CustomFieldType[] = ['Text', 'Number', 'Checkbox', 'Date'];

/** Date without time, formatted YYYY-MM-DD. */
export type IsoDate = string;

export interface User {
  id: string;
  email: string;
  displayName: string;
  timeZoneId: string;
  profileTemplate: ProfileTemplate;
  createdAt: string;
}

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  user: User;
}

export interface Tag {
  id: string;
  name: string;
  color: string | null;
}

export interface CustomFieldDefinition {
  id: string;
  name: string;
  type: CustomFieldType;
  position: number;
}

export interface CustomFieldValue {
  definitionId: string;
  value: string;
}

export interface Subtask {
  id: string;
  title: string;
  isDone: boolean;
  position: number;
}

export interface StatusTransition {
  fromStatus: TaskStatus;
  toStatus: TaskStatus;
  changedAt: string;
}

export interface TaskListItem {
  id: string;
  title: string;
  description: string | null;
  status: TaskStatus;
  priority: TaskPriority;
  deadline: IsoDate | null;
  position: number;
  createdAt: string;
  updatedAt: string;
  completedAt: string | null;
  isOverdue: boolean;
  tags: Tag[];
  subtasksTotal: number;
  subtasksDone: number;
}

export interface TaskDetails {
  id: string;
  title: string;
  description: string | null;
  notesMarkdown: string | null;
  status: TaskStatus;
  priority: TaskPriority;
  deadline: IsoDate | null;
  position: number;
  createdAt: string;
  updatedAt: string;
  completedAt: string | null;
  isOverdue: boolean;
  tags: Tag[];
  subtasks: Subtask[];
  customFields: CustomFieldValue[];
  transitions: StatusTransition[];
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface CustomFieldValueInput {
  definitionId: string;
  value: string | null;
}

export interface CreateTaskRequest {
  title: string;
  description?: string | null;
  notesMarkdown?: string | null;
  status?: TaskStatus;
  priority?: TaskPriority;
  deadline?: IsoDate | null;
  tagIds?: string[];
  customFields?: CustomFieldValueInput[];
}

export interface UpdateTaskRequest {
  title: string;
  description: string | null;
  notesMarkdown: string | null;
  status: TaskStatus;
  priority: TaskPriority;
  deadline: IsoDate | null;
  tagIds: string[];
  customFields: CustomFieldValueInput[];
}

/** Filters, sorting and paging shared by the list, board and calendar. */
export interface TaskFilters {
  status: TaskStatus[];
  priority: TaskPriority | '';
  tagIds: string[];
  deadline: DeadlineMode | 'range' | '';
  deadlineFrom: IsoDate;
  deadlineTo: IsoDate;
  q: string;
  customFieldId: string;
  customFieldValue: string;
  sort: SortField;
  dir: SortDirection | '';
}
