import { RelativeTimePipe } from './relative-time.pipe';

describe('RelativeTimePipe', () => {
  let pipe: RelativeTimePipe;

  beforeEach(() => {
    pipe = new RelativeTimePipe();
  });

  it('should create', () => {
    expect(pipe).toBeTruthy();
  });

  it('should return empty string for null', () => {
    expect(pipe.transform(null)).toBe('');
  });

  it('should return empty string for undefined', () => {
    expect(pipe.transform(undefined)).toBe('');
  });

  it('should return empty string for invalid date string', () => {
    expect(pipe.transform('not-a-date')).toBe('');
  });

  it('should return "just now" for a date less than 30 seconds ago', () => {
    const recent = new Date(Date.now() - 10 * 1000);
    expect(pipe.transform(recent.toISOString())).toBe('just now');
  });

  it('should return seconds ago for 45 seconds ago', () => {
    const past = new Date(Date.now() - 45 * 1000);
    expect(pipe.transform(past.toISOString())).toBe('45 seconds ago');
  });

  it('should return "1 minute ago" for exactly 1 minute ago', () => {
    const past = new Date(Date.now() - 60 * 1000);
    expect(pipe.transform(past.toISOString())).toBe('1 minute ago');
  });

  it('should return "5 minutes ago" for 5 minutes ago', () => {
    const past = new Date(Date.now() - 5 * 60 * 1000);
    expect(pipe.transform(past.toISOString())).toBe('5 minutes ago');
  });

  it('should return "1 hour ago" for exactly 1 hour ago', () => {
    const past = new Date(Date.now() - 60 * 60 * 1000);
    expect(pipe.transform(past.toISOString())).toBe('1 hour ago');
  });

  it('should return "3 hours ago" for 3 hours ago', () => {
    const past = new Date(Date.now() - 3 * 60 * 60 * 1000);
    expect(pipe.transform(past.toISOString())).toBe('3 hours ago');
  });

  it('should return "1 day ago" for exactly 24 hours ago', () => {
    const past = new Date(Date.now() - 24 * 60 * 60 * 1000);
    expect(pipe.transform(past.toISOString())).toBe('1 day ago');
  });

  it('should return "7 days ago" for 7 days ago', () => {
    const past = new Date(Date.now() - 7 * 24 * 60 * 60 * 1000);
    expect(pipe.transform(past.toISOString())).toBe('7 days ago');
  });

  it('should accept a Date object directly', () => {
    const past = new Date(Date.now() - 2 * 60 * 60 * 1000);
    expect(pipe.transform(past)).toBe('2 hours ago');
  });
});
