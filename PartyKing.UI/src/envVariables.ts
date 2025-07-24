class EnvVariables {
  private readonly _apiUrl: string;

  constructor() {
    this._apiUrl = "http://localhost:4200";
  }

  get apiUrl() {
    return this._apiUrl;
  }
}

export default new EnvVariables();
