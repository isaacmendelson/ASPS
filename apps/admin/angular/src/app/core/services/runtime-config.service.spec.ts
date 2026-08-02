import { RuntimeConfigService } from './runtime-config.service';

describe('RuntimeConfigService', () => {
  let service: RuntimeConfigService;

  beforeEach(() => {
    service = new RuntimeConfigService();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should return environment defaults before load()', () => {
    // Before load(), config is null — fallback to environment.ts defaults
    expect(service.apiUrl).toBeTruthy();
    expect(service.keycloakUrl).toBeTruthy();
    expect(service.keycloakRealm).toBe('asps');
    expect(service.keycloakClientId).toBe('asps-angular-admin');
  });

  it('should return config values after successful load()', async () => {
    const mockConfig = {
      apiUrl: 'https://api.example.com',
      keycloakUrl: 'https://kc.example.com',
      keycloakRealm: 'test-realm',
      keycloakClientId: 'test-client',
    };

    spyOn(window, 'fetch').and.returnValue(
      Promise.resolve({
        ok: true,
        json: () => Promise.resolve(mockConfig),
      } as Response)
    );

    await service.load();

    expect(service.apiUrl).toBe('https://api.example.com');
    expect(service.keycloakUrl).toBe('https://kc.example.com');
    expect(service.keycloakRealm).toBe('test-realm');
    expect(service.keycloakClientId).toBe('test-client');
  });

  it('should fall back to environment defaults when fetch fails', async () => {
    spyOn(window, 'fetch').and.returnValue(Promise.reject(new Error('Network error')));
    spyOn(console, 'warn');

    await service.load(); // must not throw

    expect(console.warn).toHaveBeenCalled();
    // Falls back to environment defaults
    expect(service.keycloakRealm).toBe('asps');
  });

  it('should fall back when response is not ok', async () => {
    spyOn(window, 'fetch').and.returnValue(
      Promise.resolve({
        ok: false,
        status: 404,
        json: () => Promise.reject(new Error('not found')),
      } as Response)
    );
    spyOn(console, 'warn');

    await service.load();

    expect(console.warn).toHaveBeenCalled();
    expect(service.keycloakRealm).toBe('asps');
  });
});
