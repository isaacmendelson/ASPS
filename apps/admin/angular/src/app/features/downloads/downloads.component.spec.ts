import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DownloadsComponent } from './downloads.component';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';

describe('DownloadsComponent', () => {
  let fixture: ComponentFixture<DownloadsComponent>;
  let component: DownloadsComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DownloadsComponent, NoopAnimationsModule],
    }).compileComponents();

    fixture = TestBed.createComponent(DownloadsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should render Downloads page title', () => {
    const title = fixture.nativeElement.querySelector('.page-title');
    expect(title?.textContent).toContain('Downloads');
  });

  it('should render two download cards', () => {
    const cards = fixture.nativeElement.querySelectorAll('mat-card');
    expect(cards.length).toBe(2);
  });

  it('should render Desktop Agent download card', () => {
    const content = fixture.nativeElement.textContent as string;
    expect(content).toContain('Desktop Agent');
  });

  it('should render Browser Extension download card', () => {
    const content = fixture.nativeElement.textContent as string;
    expect(content).toContain('Browser Extension');
  });

  it('should have two download buttons with href', () => {
    const links = fixture.nativeElement.querySelectorAll('a[mat-flat-button]');
    expect(links.length).toBe(2);
    links.forEach((link: HTMLAnchorElement) => {
      expect(link.getAttribute('href')).toBeTruthy();
    });
  });
});
