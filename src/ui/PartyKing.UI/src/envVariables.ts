class EnvVariables {
  private readonly _apiUrl: string;

  constructor() {
    this._apiUrl = "http://127.0.0.1:4200";
  }

  get apiUrl() {
    return this._apiUrl;
  }
}

export default new EnvVariables();
