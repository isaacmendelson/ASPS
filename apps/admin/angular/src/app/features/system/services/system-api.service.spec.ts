import { TestBed } from '@angular/core/testing';
import { SystemApiService } from './system-api.service';
import { ApiService } from '@core/services/api.service';
import { of } from 'rxjs';
import { SystemVersion, SystemHealth } from '@core/models/system.model';

const mockVersion: SystemVersion = {
  version: '1.0.0',
  isPrerelease: false,
  isPublicRelease: true,
};

const mockHealth: SystemHealth = {
  status: 'Healthy',
  timestamp: new Date().toISOString(),
};

describe('SystemApiService', () => {
  let service: SystemApiService;
  let apiSpy: jasmine.SpyObj<ApiService>;

  beforeEach(() => {
    apiSpy = jasmine.createSpyObj('ApiService', ['getOne', 'post']);
    apiSpy.getOne.and.returnValue(of({}));
    apiSpy.post.and.returnValue(of({ message: 'OK' }));

    TestBed.configureTestingModule({
      providers: [
        SystemApiService,
        { provide: ApiService, useValue: apiSpy },
      ],
    });
    service = TestBed.inject(SystemApiService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('getVersion should call api.getOne with /api/system/version', () => {
    apiSpy.getOne.and.returnValue(of(mockVersion));
    service.getVersion().subscribe((v) => {
      expect(v).toEqual(mockVersion);
    });
    expect(apiSpy.getOne).toHaveBeenCalledWith('/api/system/version');
  });

  it('getHealth should call api.getOne with /api/system/health', () => {
    apiSpy.getOne.and.returnValue(of(mockHealth));
    service.getHealth().subscribe((h) => {
      expect(h).toEqual(mockHealth);
    });
    expect(apiSpy.getOne).toHaveBeenCalledWith('/api/system/health');
  });

  it('reinitializeAsView should call api.post with reinitialize-asview endpoint', () => {
    service.reinitializeAsView().subscribe();
    expect(apiSpy.post).toHaveBeenCalledWith('/api/system/reinitialize-asview', {});
  });
});
