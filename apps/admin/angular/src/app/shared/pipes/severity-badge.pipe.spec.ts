import { SeverityBadgePipe } from './severity-badge.pipe';

describe('SeverityBadgePipe', () => {
  let pipe: SeverityBadgePipe;

  beforeEach(() => {
    pipe = new SeverityBadgePipe();
  });

  it('should create', () => {
    expect(pipe).toBeTruthy();
  });

  it('should return high cssClass for "High"', () => {
    const result = pipe.transform('High');
    expect(result.cssClass).toBe('sev-badge high');
    expect(result.label).toBe('High');
  });

  it('should return high cssClass for "Critical"', () => {
    const result = pipe.transform('Critical');
    expect(result.cssClass).toBe('sev-badge high');
  });

  it('should be case-insensitive for high', () => {
    expect(pipe.transform('HIGH').cssClass).toBe('sev-badge high');
    expect(pipe.transform('high').cssClass).toBe('sev-badge high');
  });

  it('should return medium cssClass for "Medium"', () => {
    const result = pipe.transform('Medium');
    expect(result.cssClass).toBe('sev-badge medium');
    expect(result.label).toBe('Medium');
  });

  it('should return low cssClass for "Low"', () => {
    const result = pipe.transform('Low');
    expect(result.cssClass).toBe('sev-badge low');
    expect(result.label).toBe('Low');
  });

  it('should return default cssClass for unknown value', () => {
    const result = pipe.transform('Unknown');
    expect(result.cssClass).toBe('sev-badge');
    expect(result.label).toBe('Unknown');
  });

  it('should handle null gracefully', () => {
    const result = pipe.transform(null);
    expect(result.cssClass).toBe('sev-badge');
    expect(result.label).toBe('Unknown');
  });

  it('should handle undefined gracefully', () => {
    const result = pipe.transform(undefined);
    expect(result.cssClass).toBe('sev-badge');
    expect(result.label).toBe('Unknown');
  });

  it('should handle empty string', () => {
    const result = pipe.transform('');
    expect(result.cssClass).toBe('sev-badge');
    expect(result.label).toBe('Unknown');
  });
});
