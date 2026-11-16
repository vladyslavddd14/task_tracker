import { describe, expect, it } from 'vitest';
import {
  addDays,
  addMonths,
  endOfMonth,
  isValidIsoDate,
  startOfWeek,
  todayInZone,
  weekdayIndex,
} from './dates';

describe('date arithmetic', () => {
  it('adds days across month and year boundaries', () => {
    expect(addDays('2026-12-31', 1)).toBe('2027-01-01');
    expect(addDays('2026-03-01', -1)).toBe('2026-02-28');
  });

  it('is not affected by daylight saving time changes', () => {
    // Europe switches to summer time on 29 March 2026 and back on 25 October 2026.
    expect(addDays('2026-03-28', 2)).toBe('2026-03-30');
    expect(addDays('2026-10-24', 2)).toBe('2026-10-26');
  });

  it('adds months and clamps the day for shorter months', () => {
    expect(addMonths('2026-01-31', 1)).toBe('2026-02-28');
    expect(addMonths('2026-11-15', 2)).toBe('2027-01-15');
    expect(addMonths('2026-01-15', -1)).toBe('2025-12-15');
  });

  it('finds the end of a month, including leap years', () => {
    expect(endOfMonth('2026-02-10')).toBe('2026-02-28');
    expect(endOfMonth('2028-02-10')).toBe('2028-02-29');
  });

  it('starts weeks on Monday', () => {
    expect(weekdayIndex('2026-11-02')).toBe(0); // Monday
    expect(weekdayIndex('2026-11-08')).toBe(6); // Sunday
    expect(startOfWeek('2026-11-08')).toBe('2026-11-02');
    expect(startOfWeek('2026-11-02')).toBe('2026-11-02');
  });
});

describe('todayInZone', () => {
  it('returns the calendar date of the given time zone', () => {
    const instant = new Date('2026-11-05T22:30:00Z');
    expect(todayInZone('UTC', instant)).toBe('2026-11-05');
    expect(todayInZone('Pacific/Kiritimati', instant)).toBe('2026-11-06'); // UTC+14
    expect(todayInZone('Pacific/Pago_Pago', instant)).toBe('2026-11-05'); // UTC-11
  });

  it('falls back to UTC for an unknown zone', () => {
    expect(todayInZone('Not/AZone', new Date('2026-11-05T12:00:00Z'))).toBe('2026-11-05');
  });
});

describe('isValidIsoDate', () => {
  it('accepts real dates and rejects impossible ones', () => {
    expect(isValidIsoDate('2026-02-28')).toBe(true);
    expect(isValidIsoDate('2026-02-30')).toBe(false);
    expect(isValidIsoDate('2026-2-3')).toBe(false);
    expect(isValidIsoDate('hello')).toBe(false);
  });
});
