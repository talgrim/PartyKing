export type AccountRole = 'User' | 'Admin';

// What GET /api/me of the identity service answers
export type AccountDto = {
  id: string;
  username: string;
  role: AccountRole;
  createdAt: string;
};
