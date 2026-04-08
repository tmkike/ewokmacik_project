import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';

import { Books } from './books';
import { BookService } from '../../services/book.service';

describe('Books', () => {
  let component: Books;
  let fixture: ComponentFixture<Books>;
  const bookServiceMock = {
    getBooks: () => of([]),
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Books],
      providers: [
        provideRouter([]),
        { provide: BookService, useValue: bookServiceMock },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(Books);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should clear the success message after 3 seconds', () => {
    vi.useFakeTimers();
    component.successMessage = 'Sikeres művelet';
    component.successMessageVisible = true;

    (component as any).startSuccessMessageTimer();
    vi.advanceTimersByTime(1500);

    expect(component.successMessage).toBe('');
    expect(component.successMessageVisible).toBe(false);
    vi.useRealTimers();
  });

  it('should clear the navigation message from history', () => {
    const replaceStateSpy = vi.spyOn(history, 'replaceState');

    (component as any).clearNavigationMessageFromHistory();

    expect(replaceStateSpy).toHaveBeenCalled();
  });
});
