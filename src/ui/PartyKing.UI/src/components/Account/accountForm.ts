/** Mirrors the Domain's AccountPassword.MinLength / MaxLength. */
export const MIN_ACCOUNT_PASSWORD_LENGTH = 8;
export const MAX_ACCOUNT_PASSWORD_LENGTH = 100;

/** Mirrors the Domain's AccountUsernames.MaxLength. */
export const MAX_ACCOUNT_USERNAME_LENGTH = 50;

export const errorMessage = (error: unknown, fallback: string) => (error as { message?: string })?.message || fallback;

export const inputClass = 'w-full px-2 py-2 border-1 border-gray-700 rounded-sm focus:outline-none';
export const primaryButton = 'px-3 py-2 bg-amber-600 text-slate-200 rounded-md cursor-pointer disabled:opacity-50';
export const secondaryButton = 'px-2 py-1 bg-slate-900 rounded-md cursor-pointer disabled:opacity-50';
