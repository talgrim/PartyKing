export interface AuthorizeArgs {
  body: string;
}

export interface UserModel {
  id: string;
  accessToken: string;
}

export type Authorize = (args: AuthorizeArgs) => Promise<void>;