using TaskTracker.Domain.Enums;

namespace TaskTracker.Application.Dtos;

// ----- Auth and profile -----

public record RegisterRequest(
    string Email, string Password, string DisplayName, string? TimeZoneId, ProfileTemplate? ProfileTemplate);

public record LoginRequest(string Email, string Password);

public record UpdateProfileRequest(string DisplayName, string TimeZoneId);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record ApplyTemplateRequest(ProfileTemplate Template);

public record UserDto(
    Guid Id, string Email, string DisplayName, string TimeZoneId, ProfileTemplate ProfileTemplate, DateTime CreatedAt);

public record AuthResponse(string AccessToken, DateTime ExpiresAt, UserDto User);

// ----- Tasks -----

public record CustomFieldValueInput(Guid DefinitionId, string? Value);

/// <summary>TagIds / CustomFields set to null mean "leave unchanged".</summary>
public record CreateTaskRequest(
    string Title, string? Description, string? NotesMarkdown, TaskItemStatus? Status, TaskPriority? Priority,
    DateOnly? Deadline, List<Guid>? TagIds, List<CustomFieldValueInput>? CustomFields);

public record UpdateTaskRequest(
    string Title, string? Description, string? NotesMarkdown, TaskItemStatus Status, TaskPriority Priority,
    DateOnly? Deadline, List<Guid>? TagIds, List<CustomFieldValueInput>? CustomFields);

public record ChangeStatusRequest(TaskItemStatus Status);

public record MoveTaskRequest(TaskItemStatus Status, double Position);

public record TagDto(Guid Id, string Name, string? Color);

public record SubtaskDto(Guid Id, string Title, bool IsDone, double Position);

public record StatusTransitionDto(TaskItemStatus FromStatus, TaskItemStatus ToStatus, DateTime ChangedAt);

public record CustomFieldValueDto(Guid DefinitionId, string Value);

public record TaskListItemDto(
    Guid Id, string Title, string? Description, TaskItemStatus Status, TaskPriority Priority, DateOnly? Deadline,
    double Position, DateTime CreatedAt, DateTime UpdatedAt, DateTime? CompletedAt, bool IsOverdue,
    IReadOnlyList<TagDto> Tags, int SubtasksTotal, int SubtasksDone);

public record TaskDetailsDto(
    Guid Id, string Title, string? Description, string? NotesMarkdown, TaskItemStatus Status, TaskPriority Priority,
    DateOnly? Deadline, double Position, DateTime CreatedAt, DateTime UpdatedAt, DateTime? CompletedAt, bool IsOverdue,
    IReadOnlyList<TagDto> Tags, IReadOnlyList<SubtaskDto> Subtasks, IReadOnlyList<CustomFieldValueDto> CustomFields,
    IReadOnlyList<StatusTransitionDto> Transitions);

// ----- Subtasks -----

public record SubtaskRequest(string Title, bool IsDone);

public record ReorderSubtasksRequest(List<Guid> OrderedIds);

// ----- Tags and custom fields -----

public record TagRequest(string Name, string? Color);

public record CustomFieldRequest(string Name, CustomFieldType Type, int? Position);

public record CustomFieldDto(Guid Id, string Name, CustomFieldType Type, int Position);
