export type AccountRole = 'User' | 'Admin';

// Mirrors PartyKing.Contract.V1.Accounts.AccountDto
export type AccountDto = {
  id: string;
  username: string;
  role: AccountRole;
  createdAt: string;
};

// Mirrors PartyKing.Contract.V1.Common.PagedDto
export type PagedDto<T> = {
  items: T[];
  /** The page asked for, 1-based. */
  page: number;
  pageSize: number;
  totalCount: number;
};
