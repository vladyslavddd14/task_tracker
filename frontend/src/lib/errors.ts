import { ApiError } from '../api/client';
import { t } from '../i18n';

/** Converts any error into a readable Ukrainian message. */
export function describeError(error: unknown, conflictMessage?: string): string {
  if (error instanceof ApiError) {
    switch (error.status) {
      case 400:
        return t.errors.badRequest;
      case 401:
        return t.errors.unauthorized;
      case 404:
        return t.errors.notFound;
      case 409:
        return conflictMessage ?? t.errors.conflict;
      default:
        return error.status >= 500 ? t.errors.server : t.errors.generic;
    }
  }
  if (error instanceof TypeError) {
    // fetch() rejects with a TypeError when the server cannot be reached.
    return t.errors.network;
  }
  return t.errors.generic;
}
