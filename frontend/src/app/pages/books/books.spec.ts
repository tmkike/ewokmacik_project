import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';

import { BookService } from '../../services/book.service';
import { Books } from './books';

describe('Books', () => {
  let component: Books;
  let fixture: ComponentFixture<Books>;
  const bookServiceMock = {
    getBooks: vi.fn(() => of([])),
  };

  beforeEach(async () => {
    bookServiceMock.getBooks.mockClear();

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

  it('should request filtered books from the backend', () => {
    bookServiceMock.getBooks.mockClear();
    component.filters = {
      title: 'Dune',
      author: 'Frank Herbert',
      genre: 'Science Fiction',
      available: true,
    };

    component.applyFilters();

    expect(bookServiceMock.getBooks).toHaveBeenCalledWith({
      title: 'Dune',
      author: 'Frank Herbert',
      genre: 'Science Fiction',
      available: true,
    });
  });

  it('should clear the success message after 1.5 seconds', () => {
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

  it('should label books with an active loan as borrowed', () => {
    expect(component.getBookAvailabilityLabel({
      _id: '1',
      title: 'Dune',
      author: 'Frank Herbert',
      year: 1965,
      genre: 'Science Fiction',
      available: false,
      hasActiveLoan: true,
    })).toBe('Kikölcsönözve');
  });

  it('should keep regular unavailable books distinct from borrowed ones', () => {
    expect(component.getBookAvailabilityLabel({
      _id: '1',
      title: 'Dune',
      author: 'Frank Herbert',
      year: 1965,
      genre: 'Science Fiction',
      available: false,
      hasActiveLoan: false,
    })).toBe('Nem elérhető');
  });
});
