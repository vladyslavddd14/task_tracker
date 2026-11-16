import { describe, expect, it } from 'vitest';
import { t } from '../i18n';
import {
  validateCustomFieldValue,
  validateDateRange,
  validateDisplayName,
  validateEmail,
  validatePassword,
  validateTitle,
} from './validation';

describe('validateTitle', () => {
  it('rejects an empty or whitespace-only title', () => {
    expect(validateTitle('')).toBe(t.validation.titleRequired);
    expect(validateTitle('   ')).toBe(t.validation.titleRequired);
  });

  it('accepts a normal title and a title of exactly 200 characters', () => {
    expect(validateTitle('Купити молоко')).toBeNull();
    expect(validateTitle('a'.repeat(200))).toBeNull();
  });

  it('rejects a title longer than 200 characters', () => {
    expect(validateTitle('a'.repeat(201))).toBe(t.validation.titleTooLong(200));
  });

  it('measures the length after trimming', () => {
    expect(validateTitle(`  ${'a'.repeat(200)}  `)).toBeNull();
  });
});

describe('validateEmail', () => {
  it.each(['', 'plain', 'a@b', '@b.co', 'a b@c.co'])('rejects %j', (value) => {
    expect(validateEmail(value)).toBe(t.validation.emailInvalid);
  });

  it('accepts a normal address', () => {
    expect(validateEmail('anna@example.com')).toBeNull();
  });
});

describe('validatePassword', () => {
  it('requires at least 8 characters', () => {
    expect(validatePassword('1234567')).toBe(t.validation.passwordTooShort(8));
    expect(validatePassword('12345678')).toBeNull();
  });

  it('allows at most 100 characters', () => {
    expect(validatePassword('p'.repeat(100))).toBeNull();
    expect(validatePassword('p'.repeat(101))).toBe(t.validation.passwordTooLong(100));
  });
});

describe('validateDisplayName', () => {
  it('requires a name', () => {
    expect(validateDisplayName('  ')).toBe(t.validation.nameRequired);
    expect(validateDisplayName('Анна')).toBeNull();
  });
});

describe('validateDateRange', () => {
  it('rejects a reversed range and accepts open or ordered ranges', () => {
    expect(validateDateRange('2026-11-10', '2026-11-01')).toBe(t.validation.rangeInvalid);
    expect(validateDateRange('2026-11-01', '2026-11-10')).toBeNull();
    expect(validateDateRange('2026-11-01', '')).toBeNull();
    expect(validateDateRange('', '')).toBeNull();
  });
});

describe('validateCustomFieldValue', () => {
  it('always allows a blank value (it clears the field)', () => {
    expect(validateCustomFieldValue('Number', '')).toBeNull();
    expect(validateCustomFieldValue('Date', '  ')).toBeNull();
  });

  it('checks numbers', () => {
    expect(validateCustomFieldValue('Number', '12.5')).toBeNull();
    expect(validateCustomFieldValue('Number', '-3')).toBeNull();
    expect(validateCustomFieldValue('Number', 'abc')).toBe(t.validation.numberInvalid);
    expect(validateCustomFieldValue('Number', '1,5')).toBe(t.validation.numberInvalid);
  });

  it('checks dates', () => {
    expect(validateCustomFieldValue('Date', '2026-11-05')).toBeNull();
    expect(validateCustomFieldValue('Date', '2026-13-40')).toBe(t.validation.dateInvalid);
    expect(validateCustomFieldValue('Date', '05.11.2026')).toBe(t.validation.dateInvalid);
  });

  it('limits the length of text', () => {
    expect(validateCustomFieldValue('Text', 'x'.repeat(1000))).toBeNull();
    expect(validateCustomFieldValue('Text', 'x'.repeat(1001))).not.toBeNull();
  });
});
