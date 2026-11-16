import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError, request, setUnauthorizedHandler, toQuery, tokenStore } from './client';
import { filtersToParams } from './endpoints';
import { defaultFilters } from '../features/tasks/filters';

function mockFetch(status: number, body?: unknown) {
  const fetchMock = vi.fn().mockResolvedValue(
    new Response(body === undefined ? null : JSON.stringify(body), {
      status,
      headers: { 'Content-Type': 'application/json' },
    }),
  );
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

describe('request', () => {
  beforeEach(() => tokenStore.clear());
  afterEach(() => {
    vi.unstubAllGlobals();
    setUnauthorizedHandler(null);
  });

  it('sends the stored token as a Bearer header', async () => {
    tokenStore.set('abc');
    const fetchMock = mockFetch(200, { ok: true });

    await request('GET', '/tasks');

    const [url, options] = fetchMock.mock.calls[0];
    expect(url).toBe('/api/tasks');
    expect(options.headers.Authorization).toBe('Bearer abc');
  });

  it('sends a JSON body with the right content type', async () => {
    const fetchMock = mockFetch(201, { id: '1' });

    await request('POST', '/tasks', { title: 'x' });

    const [, options] = fetchMock.mock.calls[0];
    expect(options.headers['Content-Type']).toBe('application/json');
    expect(options.body).toBe(JSON.stringify({ title: 'x' }));
  });

  it('clears the token and calls the handler on 401 of a protected request', async () => {
    tokenStore.set('expired');
    const handler = vi.fn();
    setUnauthorizedHandler(handler);
    mockFetch(401, { title: 'Unauthorized' });

    await expect(request('GET', '/tasks')).rejects.toBeInstanceOf(ApiError);

    expect(handler).toHaveBeenCalledOnce();
    expect(tokenStore.get()).toBeNull();
  });

  it('does not treat a failed login as an expired session', async () => {
    tokenStore.set('still-valid');
    const handler = vi.fn();
    setUnauthorizedHandler(handler);
    mockFetch(401, { title: 'Unauthorized' });

    await expect(request('POST', '/auth/login', {})).rejects.toMatchObject({ status: 401 });

    expect(handler).not.toHaveBeenCalled();
    expect(tokenStore.get()).toBe('still-valid');
  });

  it('turns problem details into an ApiError with field errors', async () => {
    mockFetch(400, { title: 'Validation failed', detail: 'bad', errors: { title: ['empty'] } });

    await expect(request('POST', '/tasks', {})).rejects.toMatchObject({
      status: 400,
      message: 'bad',
      errors: { title: ['empty'] },
    });
  });

  it('returns undefined for 204 No Content', async () => {
    mockFetch(204);

    await expect(request('DELETE', '/tasks/1')).resolves.toBeUndefined();
  });
});

describe('toQuery', () => {
  it('repeats array parameters and skips empty values', () => {
    expect(toQuery({ status: ['Todo', 'Done'], q: '', priority: undefined, page: 2 })).toBe(
      '?status=Todo&status=Done&page=2',
    );
    expect(toQuery({})).toBe('');
  });
});

describe('filtersToParams', () => {
  it('sends only the filters that are set', () => {
    expect(filtersToParams(defaultFilters)).toEqual({ sort: 'priority' });
  });

  it('maps every filter to the API parameter', () => {
    const params = filtersToParams({
      ...defaultFilters,
      status: ['Todo', 'InProgress'],
      priority: 'High',
      tagIds: ['t1', 't2'],
      deadline: 'overdue',
      q: '  отчет ',
      sort: 'deadline',
      dir: 'desc',
    });

    expect(params).toEqual({
      status: ['Todo', 'InProgress'],
      priority: 'High',
      tagId: ['t1', 't2'],
      deadline: 'overdue',
      q: 'отчет',
      sort: 'deadline',
      dir: 'desc',
    });
  });

  it('sends a date range instead of a deadline mode', () => {
    const params = filtersToParams({
      ...defaultFilters,
      deadline: 'range',
      deadlineFrom: '2026-11-01',
      deadlineTo: '2026-11-30',
    });

    expect(params.deadline).toBeUndefined();
    expect(params.deadlineFrom).toBe('2026-11-01');
    expect(params.deadlineTo).toBe('2026-11-30');
  });

  it('sends the custom field filter only when both field and value are set', () => {
    expect(
      filtersToParams({ ...defaultFilters, customFieldId: 'f1' }).customFieldId,
    ).toBeUndefined();
    expect(
      filtersToParams({ ...defaultFilters, customFieldId: 'f1', customFieldValue: 'acme' }),
    ).toMatchObject({ customFieldId: 'f1', customFieldValue: 'acme' });
  });
});
