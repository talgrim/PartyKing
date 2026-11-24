class EnvVariables {
  private readonly _apiUrl: string;
  private readonly _identityUrl: string;

  constructor() {
    // The container serves the UI and proxies the API under /api, so production builds use the same origin
    this._apiUrl = import.meta.env.VITE_API_URL ?? (import.meta.env.DEV ? "http://127.0.0.1:4200" : "/api");
    // Accounts live in the identity service, whose login page and API share one origin (the dev server proxies /api)
    this._identityUrl = import.meta.env.VITE_IDENTITY_URL ?? (import.meta.env.DEV ? "http://127.0.0.1:5173" : "https://auth.talgrim.dev");
  }

  get apiUrl() {
    return this._apiUrl;
  }

  get identityUrl() {
    return this._identityUrl;
  }
}

export default new EnvVariables();
