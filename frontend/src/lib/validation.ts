import { t } from '../i18n';
import { isValidIsoDate } from './dates';
import type { CustomFieldType } from '../api/types';

// The same limits as in the backend (the backend stays the source of truth).
export const LIMITS = {
  title: 200,
  description: 2000,
  notes: 20_000,
  tagName: 30,
  fieldName: 40,
  fieldValue: 1000,
  displayName: 100,
  email: 254,
  passwordMin: 8,
  passwordMax: 100,
  maxSubtasks: 50,
} as const;

/** Each validator returns an error text or null when the value is valid. */

export function validateTitle(value: string, max: number = LIMITS.title): string | null {
  const trimmed = value.trim();
  if (!trimmed) return t.validation.titleRequired;
  if (trimmed.length > max) return t.validation.titleTooLong(max);
  return null;
}

export function validateEmail(value: string): string | null {
  const trimmed = value.trim();
  if (!trimmed || trimmed.length > LIMITS.email || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(trimmed)) {
    return t.validation.emailInvalid;
  }
  return null;
}

export function validatePassword(value: string): string | null {
  if (value.length < LIMITS.passwordMin) return t.validation.passwordTooShort(LIMITS.passwordMin);
  if (value.length > LIMITS.passwordMax) return t.validation.passwordTooLong(LIMITS.passwordMax);
  return null;
}

export function validateDisplayName(value: string): string | null {
  const trimmed = value.trim();
  if (!trimmed) return t.validation.nameRequired;
  if (trimmed.length > LIMITS.displayName) return t.validation.textTooLong(LIMITS.displayName);
  return null;
}

export function validateMaxLength(value: string, max: number): string | null {
  return value.length > max ? t.validation.textTooLong(max) : null;
}

export function validateDateRange(from: string, to: string): string | null {
  if (from && to && to < from) return t.validation.rangeInvalid;
  return null;
}

/** Checks a custom field value by the field type. A blank value is always allowed (it clears the field). */
export function validateCustomFieldValue(type: CustomFieldType, value: string): string | null {
  const trimmed = value.trim();
  if (!trimmed) return null;
  switch (type) {
    case 'Number':
      return /^-?\d+(\.\d+)?$/.test(trimmed) ? null : t.validation.numberInvalid;
    case 'Date':
      return isValidIsoDate(trimmed) ? null : t.validation.dateInvalid;
    case 'Checkbox':
      return trimmed === 'true' || trimmed === 'false' ? null : t.validation.required;
    default:
      return validateMaxLength(trimmed, LIMITS.fieldValue);
  }
}
