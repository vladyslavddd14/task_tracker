import type { IsoDate } from '../api/types';

// Deadlines are calendar dates (YYYY-MM-DD) without a time zone. All date arithmetic below uses UTC
// components on purpose, so daylight saving time or the browser time zone can never shift a date.

const MS_PER_DAY = 86_400_000;

export function parseIsoDate(value: IsoDate): Date {
  const [year, month, day] = value.split('-').map(Number);
  return new Date(Date.UTC(year, month - 1, day));
}

export function toIsoDate(date: Date): IsoDate {
  return date.toISOString().slice(0, 10);
}

export function addDays(value: IsoDate, days: number): IsoDate {
  return toIsoDate(new Date(parseIsoDate(value).getTime() + days * MS_PER_DAY));
}

/** Moves to the same day of another month; the day is clamped for shorter months. */
export function addMonths(value: IsoDate, months: number): IsoDate {
  const date = parseIsoDate(value);
  const target = new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth() + months, 1));
  const lastDay = new Date(
    Date.UTC(target.getUTCFullYear(), target.getUTCMonth() + 1, 0),
  ).getUTCDate();
  target.setUTCDate(Math.min(date.getUTCDate(), lastDay));
  return toIsoDate(target);
}

export function startOfMonth(value: IsoDate): IsoDate {
  return `${value.slice(0, 7)}-01`;
}

export function endOfMonth(value: IsoDate): IsoDate {
  const date = parseIsoDate(value);
  return toIsoDate(new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth() + 1, 0)));
}

/** Index of the weekday where Monday is 0 and Sunday is 6. */
export function weekdayIndex(value: IsoDate): number {
  return (parseIsoDate(value).getUTCDay() + 6) % 7;
}

export function startOfWeek(value: IsoDate): IsoDate {
  return addDays(value, -weekdayIndex(value));
}

/** The current calendar date in the given IANA time zone (falls back to UTC). */
export function todayInZone(timeZoneId: string, now: Date = new Date()): IsoDate {
  try {
    return new Intl.DateTimeFormat('en-CA', {
      timeZone: timeZoneId,
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
    }).format(now);
  } catch {
    return toIsoDate(now);
  }
}

export function formatDate(value: IsoDate): string {
  return new Intl.DateTimeFormat('uk-UA', {
    timeZone: 'UTC',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  }).format(parseIsoDate(value));
}

export function formatDayMonth(value: IsoDate): string {
  return new Intl.DateTimeFormat('uk-UA', {
    timeZone: 'UTC',
    day: 'numeric',
    month: 'long',
  }).format(parseIsoDate(value));
}

export function formatMonthYear(value: IsoDate): string {
  const text = new Intl.DateTimeFormat('uk-UA', {
    timeZone: 'UTC',
    month: 'long',
    year: 'numeric',
  }).format(parseIsoDate(value));
  return text.charAt(0).toUpperCase() + text.slice(1);
}

export function formatWeekday(value: IsoDate): string {
  return new Intl.DateTimeFormat('uk-UA', { timeZone: 'UTC', weekday: 'short' }).format(
    parseIsoDate(value),
  );
}

/** Formats a UTC timestamp from the API in the time zone of the user. */
export function formatDateTime(isoUtc: string, timeZoneId: string): string {
  const options: Intl.DateTimeFormatOptions = {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  };
  try {
    return new Intl.DateTimeFormat('uk-UA', { ...options, timeZone: timeZoneId }).format(
      new Date(isoUtc),
    );
  } catch {
    return new Intl.DateTimeFormat('uk-UA', options).format(new Date(isoUtc));
  }
}

export function isValidIsoDate(value: string): boolean {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    return false;
  }
  return toIsoDate(parseIsoDate(value)) === value;
}
