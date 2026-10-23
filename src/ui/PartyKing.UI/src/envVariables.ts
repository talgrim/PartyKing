class EnvVariables {
  private readonly _apiUrl: string;

  constructor() {
    // The container serves the UI and proxies the API under /api, so production builds use the same origin
    this._apiUrl = import.meta.env.VITE_API_URL ?? (import.meta.env.DEV ? "http://127.0.0.1:4200" : "/api");
  }

  get apiUrl() {
    return this._apiUrl;
  }
}

export default new EnvVariables();
